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
    // 冒烟：Core 程序集可正常加载，验证测试项目引用链路畅通
    public void CoreAssembly_IsLoadable() =>
        Assert.NotNull(Assembly.Load("StockDiff.Core"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // 冒烟：空白输入回退默认地址
    public void NormalizeBaseUrl_BlankInput_FallsBackToDefault(string? raw) =>
        Assert.Equal(AppConfig.DefaultBaseUrl, AppConfig.NormalizeBaseUrl(raw));

    [Fact]
    // 冒烟：首尾空白被裁剪
    public void NormalizeBaseUrl_TrimsSurroundingWhitespace() =>
        Assert.Equal("http://192.0.2.10:7880", AppConfig.NormalizeBaseUrl("  http://192.0.2.10:7880  "));
}