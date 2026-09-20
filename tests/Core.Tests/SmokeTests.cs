// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 最小冒烟测试，验证测试框架与 Core 类库引用链路通畅，覆盖基础地址归一化行为。

using System.Reflection;
using StockDiff.Core.Config;
using Xunit;

namespace Core.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void CoreAssembly_IsLoadable() =>
        Assert.NotNull(Assembly.Load("StockDiff.Core"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeBaseUrl_BlankInput_FallsBackToDefault(string? raw) =>
        Assert.Equal(AppConfig.DefaultBaseUrl, AppConfig.NormalizeBaseUrl(raw));

    [Fact]
    public void NormalizeBaseUrl_TrimsSurroundingWhitespace() =>
        Assert.Equal("http://192.168.13.8:7880", AppConfig.NormalizeBaseUrl("  http://192.168.13.8:7880  "));
}