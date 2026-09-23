// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: F7 CSV 导出端到端集成测试：以 stub HTTP 处理器驱动「登录 → 携带令牌拉取受保护接口 →
//       导出 CSV」全链路，验证令牌透传、JSON 反序列化、列派生、BOM、转义与真实落盘在链路下一致。

using System.Net;
using System.Text;
using StockDiff.Core.Api;
using StockDiff.Core.Export;
using StockDiff.Core.Models;
using Xunit;

namespace Core.Tests;

public sealed class CsvExporterIntegrationTests
{
    private const string LoginJson = """{"code":0,"message":"ok","data":{"token":"E2E-TOKEN"}}""";

    private const string EmptyJson = """{"code":0,"message":"ok","data":[]}""";

    // 两行数据：储位含逗号 "A,B"，用于验证转义贯穿「后端 → 模型 → 表格投影 → CSV」整条链路
    private const string DiffJson = """
        {"code":0,"message":"ok","data":[
          {"material_code":"AC001","diff_type":"储位不一致","warehouse_type":"fc","warehouse_qty":2640,
           "wms_qty":2640,"qty_diff":0,"location":"3-21-1-2","warehouse_no":"100B",
           "warehouse_hold":"N","wms_hold":"N","warehouse_expiry":"20271124","wms_expiry":"20271124"},
          {"material_code":"AC002","diff_type":"数量不一致","warehouse_type":"asrs","warehouse_qty":1,
           "wms_qty":2,"qty_diff":-1,"location":"A,B","warehouse_no":"100B",
           "warehouse_hold":"N","wms_hold":"N","warehouse_expiry":"20271124","wms_expiry":"20271124"}
        ]}
        """;

    private static ApiClient NewClient(string diffJson) =>
        new("http://127.0.0.1:7880", new StubHandler(LoginJson, diffJson));

    [Fact]
    // 端到端：登录 → 拉取受保护接口 → 导出 CSV，令牌透传、BOM、12 列表头与转义均正确
    public async Task LoginThenFetchThenExport_ProducesExcelReadyCsv_EndToEnd()
    {
        var handler = new StubHandler(LoginJson, DiffJson);
        var client = new ApiClient("http://127.0.0.1:7880", handler);

        var token = await client.LoginAsync("000", "0000");
        var rows = await client.FetchStockDiffAsync("fc", true, false);

        using var stream = new MemoryStream();
        CsvExporter.Write(stream, rows);

        var bytes = stream.ToArray();
        Assert.Equal("E2E-TOKEN", token);
        Assert.Equal("Bearer E2E-TOKEN", handler.LastAuthorization);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());

        var text = Encoding.UTF8.GetString(bytes);
        var records = text.TrimStart('\uFEFF').TrimEnd('\r', '\n').Split("\r\n");

        Assert.Equal("物料编码,异常种类,仓库类型,仓库数量,WMS数量,差异数量,储位,WMS库位,仓库冻结,WMS冻结,仓库效期,WMS效期", records[0]);
        Assert.Equal(3, records.Length);
        Assert.Equal("AC001", records[1].Split(',')[0]);
        Assert.Contains("\"A,B\"", records[2]);
        Assert.Equal("-1", records[2].Split(',')[5]);
    }

    [Fact]
    // 端到端：导出写入真实文件（File.Create）→ 回读，验证 BOM 落盘与行数可被 Excel 正常识别
    public async Task Export_WritesRealFileWithBom_EndToEnd()
    {
        var client = NewClient(DiffJson);
        await client.LoginAsync("000", "0000");
        var rows = await client.FetchStockDiffAsync("all", false, false);

        var path = Path.Combine(Path.GetTempPath(), $"kc-csv-e2e-{Guid.NewGuid():N}.csv");
        try
        {
            using (var file = File.Create(path))
            {
                CsvExporter.Write(file, rows);
            }

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
            Assert.Equal(3, File.ReadAllLines(path).Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    // 端到端异常：链路返回空数据 → 导出不产生任何内容（App 层据此提示「暂无数据」）
    public async Task LoginThenFetch_EmptyData_WritesNothing_EndToEnd()
    {
        var client = NewClient(EmptyJson);
        await client.LoginAsync("000", "0000");

        var rows = await client.FetchStockDiffAsync("all", false, false);
        using var stream = new MemoryStream();
        CsvExporter.Write(stream, rows);

        Assert.Empty(rows);
        Assert.Empty(stream.ToArray());
    }

    // 测试替身：按请求路径返回登录 / 库存差异 JSON，并记录最近一次 Authorization 头
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _loginJson;
        private readonly string _diffJson;

        public StubHandler(string loginJson, string diffJson)
        {
            _loginJson = loginJson;
            _diffJson = diffJson;
        }

        public string? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastAuthorization = request.Headers.Authorization?.ToString();
            var isLogin = request.RequestUri!.AbsolutePath.EndsWith("/auth/login", StringComparison.Ordinal);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(isLogin ? _loginJson : _diffJson, Encoding.UTF8, "application/json")
            });
        }
    }
}