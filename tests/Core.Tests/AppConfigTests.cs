// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: AppConfig 单元测试，锁定默认地址兜底、空白裁剪行为与对外常量、超时、版本号契约。

using StockDiff.Core.Config;
using Xunit;

namespace Core.Tests;

public sealed class AppConfigTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeBaseUrl_Blank_FallsBackToDefault(string? raw) =>
        Assert.Equal(AppConfig.DefaultBaseUrl, AppConfig.NormalizeBaseUrl(raw));

    [Theory]
    [InlineData("  http://10.0.0.1:8080  ", "http://10.0.0.1:8080")]
    [InlineData("http://10.0.0.1:8080/", "http://10.0.0.1:8080/")]
    public void NormalizeBaseUrl_TrimsOuterWhitespaceOnly(string raw, string expected) =>
        Assert.Equal(expected, AppConfig.NormalizeBaseUrl(raw));

    [Fact]
    public void Constants_MatchContract()
    {
        Assert.Equal("http://127.0.0.1:7880", AppConfig.DefaultBaseUrl);
        Assert.Equal("/api/v1", AppConfig.ApiPrefix);
        Assert.Equal("库存差异比对系统", AppConfig.AppName);
    }

    [Fact]
    public void Timeouts_MatchContract()
    {
        Assert.Equal(TimeSpan.FromSeconds(20), AppConfig.LoginTimeout);
        Assert.Equal(TimeSpan.FromSeconds(60), AppConfig.FetchTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), AppConfig.ConnectTestTimeout);
    }

    [Fact]
    public void Version_IsNonEmptySemanticVersion() =>
        Assert.Matches(@"^\d+\.\d+\.\d+", AppConfig.Version);
}