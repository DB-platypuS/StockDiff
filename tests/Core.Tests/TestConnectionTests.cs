// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: F3 连接测试单元测试，覆盖地址解析（端口兜底/非法）、连通成功、拒连、DNS 失败、超时与用户取消。

using System.Net.Sockets;
using StockDiff.Core.Api;
using Xunit;

namespace Core.Tests;

public sealed class TestConnectionTests
{
    private const string BaseUrl = "http://127.0.0.1:7880";

    // 测试辅助：注入替身连接器构造待测客户端，全程不发起真实网络
    private static ApiClient NewClient(FakeConnector connector, string baseUrl = BaseUrl) => new(baseUrl, connector);

    [Theory]
    [InlineData("http://127.0.0.1:7880", "127.0.0.1", 7880)]
    [InlineData("http://example.com", "example.com", 80)]
    [InlineData("https://example.com", "example.com", 443)]
    [InlineData("http://example.com/", "example.com", 80)]
    [InlineData("  http://10.0.0.1:9090  ", "10.0.0.1", 9090)]
    // 边界：解析 host 与端口；无端口按 scheme 兜底 80/443；首尾空白被 Trim
    public void ResolveHostPort_ReturnsHostAndEffectivePort(string url, string host, int port)
    {
        var (h, p) = ApiClient.ResolveHostPort(url);

        Assert.Equal(host, h);
        Assert.Equal(port, p);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    // 异常：空或非法地址抛 UriFormatException
    public void ResolveHostPort_Invalid_ThrowsUriFormatException(string? url) =>
        Assert.Throws<UriFormatException>(() => ApiClient.ResolveHostPort(url));

    [Fact]
    // 正常流程：连接成功不抛异常，且使用解析出的 host/port
    public async Task TestConnectionAsync_Reachable_CompletesAndUsesResolvedEndpoint()
    {
        var connector = new FakeConnector((_, _, _) => Task.CompletedTask);
        var client = NewClient(connector);

        await client.TestConnectionAsync();

        Assert.Equal("127.0.0.1", connector.LastHost);
        Assert.Equal(7880, connector.LastPort);
    }

    [Fact]
    // 异常：连接被拒 → ApiException 且含「连接被拒绝」建议
    public async Task TestConnectionAsync_ConnectionRefused_ThrowsWithRefusedHint()
    {
        var client = NewClient(new FakeConnector(
            (_, _, _) => throw new SocketException((int)SocketError.ConnectionRefused)));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.TestConnectionAsync());

        Assert.Contains("连接被拒绝", ex.Message);
    }

    [Fact]
    // 异常：DNS 解析失败 → ApiException 且含「无法连接到服务器」建议
    public async Task TestConnectionAsync_DnsFailure_ThrowsWithUnreachableHint()
    {
        var client = NewClient(new FakeConnector(
            (_, _, _) => throw new SocketException((int)SocketError.HostNotFound)));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.TestConnectionAsync());

        Assert.Contains("无法连接到服务器", ex.Message);
    }

    [Fact]
    // 异常：连接超时（用户未取消）→ ApiException 且含「请求超时」建议
    public async Task TestConnectionAsync_Timeout_WrapsWithTimeoutHint()
    {
        var client = NewClient(new FakeConnector(
            (_, _, _) => throw new OperationCanceledException("simulated timeout")));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.TestConnectionAsync());

        Assert.Contains("请求超时", ex.Message);
    }

    [Fact]
    // 异常：非法接口地址 → ApiException 且含「URL错误」建议，且不发起连接
    public async Task TestConnectionAsync_InvalidBaseUrl_ThrowsWithUrlHint()
    {
        var connector = new FakeConnector((_, _, _) => Task.CompletedTask);
        var client = NewClient(connector, "not-a-url");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.TestConnectionAsync());

        Assert.Contains("URL错误", ex.Message);
        Assert.Null(connector.LastHost);
    }

    [Fact]
    // 异常：用户主动取消 → 透传 OperationCanceledException，不包装为 ApiException
    public async Task TestConnectionAsync_UserCancel_PropagatesOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var client = NewClient(new FakeConnector((_, _, _) => throw new OperationCanceledException(cts.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.TestConnectionAsync(cts.Token));
    }

    [Fact]
    // 异常：非拒连/非 DNS 的套接字错误不追加排查建议，仍包装为 ApiException
    public async Task TestConnectionAsync_OtherSocketError_WrapsWithoutHint()
    {
        var client = NewClient(new FakeConnector(
            (_, _, _) => throw new SocketException((int)SocketError.ConnectionReset)));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.TestConnectionAsync());

        Assert.DoesNotContain("💡", ex.Message);
    }

    [Fact]
    // 边界：地址带路径与查询串时只取 host 与端口，不影响可达性探测
    public void ResolveHostPort_WithPathAndQuery_ExtractsHostAndPort()
    {
        var (host, port) = ApiClient.ResolveHostPort("http://example.com:8080/api/v1?x=1");

        Assert.Equal("example.com", host);
        Assert.Equal(8080, port);
    }

    // 测试替身：记录最近一次 host/port，并按行为委托返回或抛异常
    private sealed class FakeConnector : ITcpConnector
    {
        private readonly Func<string, int, CancellationToken, Task> _behavior;

        public FakeConnector(Func<string, int, CancellationToken, Task> behavior) => _behavior = behavior;

        public string? LastHost { get; private set; }
        public int LastPort { get; private set; }

        public Task ConnectAsync(string host, int port, CancellationToken ct)
        {
            LastHost = host;
            LastPort = port;
            return _behavior(host, port, ct);
        }
    }
}