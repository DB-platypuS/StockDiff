// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 集成测试：以真实回环 TCP 服务端驱动「设置接口地址 → 登录」端到端链路，
//       验证 URL 拼接、真实 HTTP 往返、JSON 反序列化与令牌落地。

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using StockDiff.Core.Api;
using StockDiff.Core.Config;
using Xunit;

namespace Core.Tests;

public sealed class LoginIntegrationTests
{
    [Fact]
    // 端到端：BaseUrlSetter 切换地址后，LoginAsync 请求真实命中新地址并取回令牌
    public async Task SetBaseUrl_ThenLogin_HitsNewAddressEndToEnd()
    {
        using var server = new LoopbackServer("""{"code":0,"message":"success","data":{"token":"E2E-T"}}""");
        var client = new ApiClient("http://127.0.0.1:1");
        var store = new InMemoryStore();

        BaseUrlSetter.SetBaseUrl(client, store, server.BaseUrl + "/");
        var token = await client.LoginAsync("000", "0000");

        Assert.Equal("E2E-T", token);
        Assert.Equal("E2E-T", client.Token);
        Assert.Equal(server.BaseUrl + "/", store.Saved);
        Assert.StartsWith("POST /api/v1/auth/login", server.RequestLine);
    }

    [Fact]
    // 端到端异常：服务端返回业务错误码时透出 message，且不写入令牌
    public async Task Login_BusinessError_PropagatesMessageEndToEnd()
    {
        using var server = new LoopbackServer("""{"code":1001,"message":"账号或密码错误","data":null}""");
        var client = new ApiClient(server.BaseUrl);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "bad"));

        Assert.Equal("账号或密码错误", ex.Message);
        Assert.Equal("", client.Token);
    }

    // 内存版地址存储，模拟持久化回读
    private sealed class InMemoryStore : IBaseUrlStore
    {
        public string? Saved { get; private set; }
        public string? Load() => Saved;
        public bool Save(string baseUrl) { Saved = baseUrl; return true; }
    }

    // 极简回环 HTTP 服务端：接受一次连接，记录请求行后回写固定 JSON
    private sealed class LoopbackServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly string _body;

        public string BaseUrl { get; }
        public string? RequestLine { get; private set; }

        public LoopbackServer(string body)
        {
            _body = body;
            _listener.Start();
            BaseUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _ = ServeOnceAsync();
        }

        // 读取请求首段，记录请求行后回写 200 + JSON；异常只记日志，由断言暴露失败
        private async Task ServeOnceAsync()
        {
            try
            {
                using var conn = await _listener.AcceptTcpClientAsync();
                using var stream = conn.GetStream();
                var buffer = new byte[8192];
                var read = await stream.ReadAsync(buffer);
                var text = Encoding.ASCII.GetString(buffer, 0, read);
                RequestLine = text.Split("\r\n", 2)[0];

                var payload = Encoding.UTF8.GetBytes(_body);
                var header = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n" +
                             $"Content-Length: {payload.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
                await stream.WriteAsync(payload);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[集成测试] 服务端异常: {ex.Message}");
            }
        }

        public void Dispose() => _listener.Stop();
    }
}