// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: F3 连接测试集成测试，用真实回环 TCP 服务端驱动默认 TcpConnector，
//       验证「公有构造函数 → 真实 socket」链路可达，以及目标端口关闭时拒连建议的映射。

using System.Net;
using System.Net.Sockets;
using StockDiff.Core.Api;
using Xunit;

namespace Core.Tests;

public sealed class TestConnectionIntegrationTests
{
    [Fact]
    // 端到端：默认构造函数走真实 TcpConnector，对正在监听的回环端口应成功返回（不抛异常即通过）
    public async Task TestConnectionAsync_RealLoopbackListener_Succeeds()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var client = new ApiClient($"http://127.0.0.1:{port}");

        await client.TestConnectionAsync();
    }

    [Fact]
    // 端到端异常：目标端口已关闭 → 真实 SocketException 经映射为含「连接被拒绝」的 ApiException
    public async Task TestConnectionAsync_ClosedPort_ThrowsWithRefusedHint()
    {
        var port = ReserveClosedPort();
        var client = new ApiClient($"http://127.0.0.1:{port}");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.TestConnectionAsync());

        Assert.Contains("连接被拒绝", ex.Message);
    }

    // 先占用再释放端口，得到一个确定无人监听的端口号，避免与随机端口冲突
    private static int ReserveClosedPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
