// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: F4 受保护接口端到端集成测试，以真实回环 HTTP 服务端驱动「登录 → 携带令牌拉取库存差异」全链路，
//       验证令牌透传、查询串拼装、JSON 反序列化与 401 会话失效的真实往返行为（非 stub）。

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using StockDiff.Core.Api;
using StockDiff.Core.Convert;
using StockDiff.Core.Table;
using Xunit;

namespace Core.Tests;

public sealed class FetchIntegrationTests
{
    private const string LoginBody = """{"code":0,"message":"success","data":{"token":"E2E-T"}}""";

    private const string DiffBody = """
        {"code":0,"message":"success","data":[
          {"material_code":"AC04672026013030856","diff_type":"储位不一致","warehouse_type":"fc",
           "warehouse_qty":2640,"wms_qty":2640,"qty_diff":0,"location":"3-21-1-2","warehouse_no":"100B",
           "warehouse_hold":"N","wms_hold":"N","warehouse_expiry":"20271124","wms_expiry":"20271124"}
        ]}
        """;

    [Fact]
    // 端到端：登录取得令牌后，同一客户端拉取受保护接口，令牌以 Bearer 透传且数据正确反序列化
    public async Task LoginThenFetch_ProtectedApi_EndToEnd()
    {
        using var server = new LoopbackRouter(new()
        {
            ["/api/v1/auth/login"] = (200, LoginBody),
            ["/api/v1/stock/diff"] = (200, DiffBody)
        });
        var client = new ApiClient(server.BaseUrl);

        await client.LoginAsync("000", "0000");
        var rows = await client.FetchStockDiffAsync("fc", true, false);

        var row = Assert.Single(rows);
        Assert.Equal("AC04672026013030856", row.MaterialCode);
        Assert.Equal("2640", row.WarehouseQty.GetRawText());
        Assert.Equal("方仓", Converters.WarehouseLabelFromCode(row.WarehouseType));

        var diff = Assert.Single(server.Captured.Where(r => r.Path == "/api/v1/stock/diff"));
        Assert.Equal("Bearer E2E-T", diff.Authorization);
        Assert.Equal("?warehouse=fc&compare_hold=true&compare_expiry=false", diff.Query);
    }

    [Fact]
    // 端到端异常：受保护接口真实返回 401 → UnauthorizedException，且客户端令牌被清空（会话失效）
    public async Task Fetch_ProtectedApiReturns401_EndToEnd_ClearsSession()
    {
        using var server = new LoopbackRouter(new()
        {
            ["/api/v1/auth/login"] = (200, LoginBody),
            ["/api/v1/stock/diff"] = (401, "{}")
        });
        var client = new ApiClient(server.BaseUrl);

        await client.LoginAsync("000", "0000");
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => client.FetchStockDiffAsync("all", false, false));

        Assert.Equal("", client.Token);
    }

    [Fact]
    // 端到端：登录 → 拉取受保护接口 → 投影为表格模型，验证 12 列、表头完整、差异数量右对齐
    public async Task LoginThenFetch_ProjectsToTwelveColumnGrid_EndToEnd()
    {
        using var server = new LoopbackRouter(new()
        {
            ["/api/v1/auth/login"] = (200, LoginBody),
            ["/api/v1/stock/diff"] = (200, DiffBody)
        });
        var client = new ApiClient(server.BaseUrl);

        await client.LoginAsync("000", "0000");
        var rows = await client.FetchStockDiffAsync("all", false, false);
        var grid = TableGrid.From(rows);

        Assert.Equal(12, grid.ColumnCount);
        Assert.Equal(rows.Count, grid.RowCount);
        Assert.False(grid.IsEmpty);
        Assert.Equal(TableColumns.Headers(), grid.Headers.ToArray());
        Assert.Equal("AC04672026013030856", grid.Rows[0][0]);
        Assert.Equal("方仓", grid.Rows[0][2]);
        Assert.Equal(ColumnAlign.Right, grid.Aligns[5]);
    }

    // 一次已处理请求的关键信息
    private sealed record Captured(string Path, string Query, string? Authorization);

    // 回环 HTTP 服务端：按路径路由响应，可连续处理多次请求（Connection: close 使每次请求独立成连接），
    // 并记录每次请求的路径、查询串与 Authorization 头
    private sealed class LoopbackRouter : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Dictionary<string, (int Status, string Body)> _routes;
        private readonly List<Captured> _captured = new();

        public string BaseUrl { get; }

        public IReadOnlyList<Captured> Captured
        {
            get { lock (_captured) { return _captured.ToArray(); } }
        }

        public LoopbackRouter(Dictionary<string, (int Status, string Body)> routes)
        {
            _routes = routes;
            _listener.Start();
            BaseUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _ = AcceptLoopAsync();
        }

        // 持续接受连接直到监听器被停止；每个连接独立处理，异常只记日志由断言暴露
        private async Task AcceptLoopAsync()
        {
            while (true)
            {
                TcpClient conn;
                try
                {
                    conn = await _listener.AcceptTcpClientAsync();
                }
                catch (Exception)
                {
                    return;
                }

                _ = HandleAsync(conn);
            }
        }

        private async Task HandleAsync(TcpClient conn)
        {
            using (conn)
            {
                try
                {
                    using var stream = conn.GetStream();
                    var head = await ReadHeadAsync(stream);
                    var (path, query) = SplitTarget(head);
                    lock (_captured)
                    {
                        _captured.Add(new Captured(path, query, ReadHeader(head, "Authorization")));
                    }

                    var (status, body) = _routes.TryGetValue(path, out var route)
                        ? route
                        : (404, """{"code":404,"message":"not found","data":null}""");
                    await WriteAsync(stream, status, body);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[集成测试] 连接处理异常: {ex.Message}");
                }
            }
        }

        // 读取请求头（直至空行），并按 Content-Length 读完正文，避免服务端提前关闭导致客户端 RST
        private static async Task<string> ReadHeadAsync(Stream stream)
        {
            var raw = new List<byte>();
            var buffer = new byte[4096];
            var headEnd = -1;
            var head = "";

            while (headEnd < 0)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                {
                    return Encoding.ASCII.GetString(raw.ToArray());
                }

                raw.AddRange(buffer.Take(read));
                head = Encoding.ASCII.GetString(raw.ToArray());
                headEnd = head.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            }

            var contentLength = ParseContentLength(head);
            var headBytes = headEnd + 4;
            while (contentLength > 0 && raw.Count - headBytes < contentLength)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                {
                    break;
                }

                raw.AddRange(buffer.Take(read));
            }

            return head[..headEnd];
        }

        // 从请求头解析 Content-Length，缺失或不可解析时返回 0
        private static int ParseContentLength(string head)
        {
            const string name = "Content-Length:";
            foreach (var line in head.Split("\r\n"))
            {
                if (line.StartsWith(name, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(line[name.Length..].Trim(), out var length))
                {
                    return length;
                }
            }

            return 0;
        }

        // 从请求行提取路径与查询串（含前导 ?，无则为空串）
        private static (string Path, string Query) SplitTarget(string head)
        {
            var requestLine = head.Split("\r\n", 2)[0];
            var parts = requestLine.Split(' ');
            var target = parts.Length > 1 ? parts[1] : "/";
            var mark = target.IndexOf('?');
            return mark < 0 ? (target, "") : (target[..mark], target[mark..]);
        }

        // 提取指定请求头的值，缺失时返回 null
        private static string? ReadHeader(string head, string name)
        {
            var prefix = name + ":";
            foreach (var line in head.Split("\r\n"))
            {
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return line[prefix.Length..].Trim();
                }
            }

            return null;
        }

        // 回写 HTTP 响应并声明 Connection: close，使每次请求独立成连接
        private static async Task WriteAsync(Stream stream, int status, string body)
        {
            var payload = Encoding.UTF8.GetBytes(body);
            var header = $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\n" +
                         "Content-Type: application/json\r\n" +
                         $"Content-Length: {payload.Length}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
            await stream.WriteAsync(payload);
            await stream.FlushAsync();
        }

        public void Dispose() => _listener.Stop();
    }
}