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
    public void UserCancelled_ReturnsOriginalTextOnly()
    {
        var text = NetworkErrorMapper.Map(new TaskCanceledException("请求已取消"), userCancelled: true);
        Assert.Equal("请求已取消", text);
    }

    [Theory]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(OperationCanceledException))]
    [InlineData(typeof(TimeoutException))]
    public void Timeout_AppendsTimeoutHint(Type type)
    {
        var ex = (Exception)Activator.CreateInstance(type, "boom")!;
        var text = NetworkErrorMapper.Map(ex);
        Assert.Contains("请求超时", text);
        Assert.Contains("boom", text);
    }

    [Fact]
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
    public void DnsFailure_AppendsUnreachableHint(SocketError error)
    {
        var text = NetworkErrorMapper.Map(new SocketException((int)error));
        Assert.Contains("无法连接到服务器", text);
    }

    [Fact]
    public void HttpRequestException_UnwrapsInnerSocketException()
    {
        var ex = new HttpRequestException("发送请求时出错", new SocketException((int)SocketError.ConnectionRefused));
        var text = NetworkErrorMapper.Map(ex);
        Assert.Contains("连接被拒绝", text);
        Assert.Contains("发送请求时出错", text);
    }

    [Fact]
    public void UriFormatException_ReturnsUrlHintOnly() =>
        Assert.Equal("💡 URL错误，请检查接口地址配置", NetworkErrorMapper.Map(new UriFormatException("bad uri")));

    [Fact]
    public void UnknownException_ReturnsOriginalTextOnly()
    {
        var text = NetworkErrorMapper.Map(new InvalidOperationException("something wrong"));
        Assert.Equal("something wrong", text);
    }
}