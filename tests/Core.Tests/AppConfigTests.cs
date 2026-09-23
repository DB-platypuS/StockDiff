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
    // 空白 / 空串 / null 地址统一回退默认地址
    public void NormalizeBaseUrl_Blank_FallsBackToDefault(string? raw) =>
        Assert.Equal(AppConfig.DefaultBaseUrl, AppConfig.NormalizeBaseUrl(raw));

    [Theory]
    [InlineData("  http://10.0.0.1:8080  ", "http://10.0.0.1:8080")]
    [InlineData("http://10.0.0.1:8080/", "http://10.0.0.1:8080/")]
    // 仅裁剪首尾空白，其余内容（含末尾斜杠）保持原样
    public void NormalizeBaseUrl_TrimsOuterWhitespaceOnly(string raw, string expected) =>
        Assert.Equal(expected, AppConfig.NormalizeBaseUrl(raw));

    [Fact]
    // 锁定对外常量的取值契约
    public void Constants_MatchContract()
    {
        Assert.Equal("http://127.0.0.1:7880", AppConfig.DefaultBaseUrl);
        Assert.Equal("/api/v1", AppConfig.ApiPrefix);
        Assert.Equal("库存差异比对系统", AppConfig.AppName);
        Assert.Equal("KC_STOCKDIFF_BASE_URL", AppConfig.BaseUrlEnvVar);
    }

    [Fact]
    // 锁定数据目录下的文件名与后缀契约（配置文件名 / 日志目录名 / 原子写临时后缀）
    public void DataFileConstants_MatchContract()
    {
        Assert.Equal("kc-stock-diff", AppConfig.DataDirName);
        Assert.Equal("settings.json", AppConfig.SettingsFileName);
        Assert.Equal("logs", AppConfig.LogDirName);
        Assert.Equal(".tmp", AppConfig.TempFileSuffix);
        Assert.Equal("app.log", AppConfig.LogFileName);
    }

    [Fact]
    // 锁定三个超时值：登录 20s / 拉取 60s / 连接测试 5s
    public void Timeouts_MatchContract()
    {
        Assert.Equal(TimeSpan.FromSeconds(20), AppConfig.LoginTimeout);
        Assert.Equal(TimeSpan.FromSeconds(60), AppConfig.FetchTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), AppConfig.ConnectTestTimeout);
    }

    [Fact]
    // 版本号应为语义化版本（x.y.z 开头）
    public void Version_IsNonEmptySemanticVersion() =>
        Assert.Matches(@"^\d+\.\d+\.\d+", AppConfig.Version);
}