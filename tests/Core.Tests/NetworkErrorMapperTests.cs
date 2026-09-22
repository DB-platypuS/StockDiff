// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: NetworkErrorMapper 单元测试，覆盖用户取消、超时、拒连、DNS 失败、URL 错误与未知异常。

using System.Net.Sockets;
using StockDiff.Core.Api;
using Xunit;

namespace Core.Tests;

public sealed class NetworkErrorMapperTests
{
    [Fact]
    // 用户主动取消：只返回原文案，不追加超时建议
    public void UserCancelled_ReturnsOriginalTextOnly()
    {
        var text = NetworkErrorMapper.Map(new TaskCanceledException("请求已取消"), userCancelled: true);
        Assert.Equal("请求已取消", text);
    }

    [Fact]
    // 边界：userCancelled 仅影响超时分支，套接字类错误仍追加排查建议
    public void UserCancelled_StillAppendsSocketHint()
    {
        var text = NetworkErrorMapper.Map(
            new SocketException((int)SocketError.ConnectionRefused), userCancelled: true);

        Assert.Contains("连接被拒绝", text);
    }

    [Theory]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(OperationCanceledException))]
    [InlineData(typeof(TimeoutException))]
    // 超时类异常：保留原文案并追加「请求超时」排查建议
    public void Timeout_AppendsTimeoutHint(Type type)
    {
        var ex = (Exception)Activator.CreateInstance(type, "boom")!;
        var text = NetworkErrorMapper.Map(ex);
        Assert.Contains("请求超时", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    // 连接被拒：追加服务器 / 端口 / 监听的排查建议
    public void ConnectionRefused_AppendsRefusedHint()
    {
        var ex = new SocketException((int)SocketError.ConnectionRefused);
        var text = NetworkErrorMapper.Map(ex);
        Assert.Contains("连接被拒绝", text);
    }

    [Theory]
    [InlineData(SocketError.HostNotFound)]
    [InlineData(SocketError.NoData)]
    [InlineData(SocketError.TryAgain)]
    // DNS 解析失败类错误：追加「无法连接到服务器」建议
    public void DnsFailure_AppendsUnreachableHint(SocketError error)
    {
        var text = NetworkErrorMapper.Map(new SocketException((int)error));
        Assert.Contains("无法连接到服务器", text);
    }

    [Fact]
    // HttpRequestException 需沿 InnerException 找到 SocketException 才能命中拒连分支
    public void HttpRequestException_UnwrapsInnerSocketException()
    {
        var ex = new HttpRequestException("发送请求时出错", new SocketException((int)SocketError.ConnectionRefused));
        var text = NetworkErrorMapper.Map(ex);
        Assert.Contains("连接被拒绝", text);
        Assert.Contains("发送请求时出错", text);
    }

    [Fact]
    // URL 格式错误：只返回固定的 URL 排查提示
    public void UriFormatException_ReturnsUrlHintOnly() =>
        Assert.Equal("💡 URL错误，请检查接口地址配置", NetworkErrorMapper.Map(new UriFormatException("bad uri")));

    [Fact]
    // 未知异常：不追加任何建议，原样返回
    public void UnknownException_ReturnsOriginalTextOnly()
    {
        var text = NetworkErrorMapper.Map(new InvalidOperationException("something wrong"));
        Assert.Equal("something wrong", text);
    }

    [Fact]
    public void OtherSocketError_AddsNoHint()
    {
        var text = NetworkErrorMapper.Map(new SocketException((int)SocketError.ConnectionReset));
        Assert.DoesNotContain("💡", text);
    }

    [Fact]
    public void DeeplyNestedSocket_IsUnwrapped()
    {
        var ex = new InvalidOperationException("outer",
            new AggregateException(new SocketException((int)SocketError.HostNotFound)));
        Assert.Contains("无法连接到服务器", NetworkErrorMapper.Map(ex));
    }
}