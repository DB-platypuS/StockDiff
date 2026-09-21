// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: Converters 单元测试，覆盖仓库代码标签互转与数量字段转字符串的全部边界。

using System.Text.Json;
using StockDiff.Core.Convert;
using Xunit;

namespace Core.Tests;

public sealed class ConvertersTests
{
    [Theory]
    [InlineData("fc", "方仓")]
    [InlineData("asrs", "立库")]
    [InlineData("all", "全部")]
    [InlineData("FC", "方仓")]
    [InlineData("ASRS", "立库")]
    [InlineData("unknown", "全部")]
    [InlineData("", "全部")]
    [InlineData(null, "全部")]
    public void WarehouseLabelFromCode_MapsAndFallsBack(string? code, string expected) =>
        Assert.Equal(expected, Converters.WarehouseLabelFromCode(code));

    [Theory]
    [InlineData("方仓", "fc")]
    [InlineData("立库", "asrs")]
    [InlineData("全部", "all")]
    [InlineData("未知", "all")]
    [InlineData("", "all")]
    [InlineData(null, "all")]
    public void WarehouseCodeFromLabel_MapsAndFallsBack(string? label, string expected) =>
        Assert.Equal(expected, Converters.WarehouseCodeFromLabel(label));

    [Fact]
    public void NumberToString_Undefined_ReturnsEmpty() =>
        Assert.Equal("", Converters.NumberToString(default(JsonElement)));

    [Fact]
    public void NumberToString_Null_ReturnsEmpty() =>
        Assert.Equal("", Converters.NumberToString(Parse("null")));

    [Theory]
    [InlineData("0")]
    [InlineData("2640")]
    [InlineData("-1")]
    public void NumberToString_Integer_KeepsRawText(string raw) =>
        Assert.Equal(raw, Converters.NumberToString(Parse(raw)));

    [Fact]
    public void NumberToString_LargeInteger_KeepsPrecision()
    {
        const string raw = "123456789012345678901234567890";
        Assert.Equal(raw, Converters.NumberToString(Parse(raw)));
    }

    [Fact]
    public void NumberToString_Decimal_KeepsTrailingZeros() =>
        Assert.Equal("12.50", Converters.NumberToString(Parse("12.50")));

    [Fact]
    public void NumberToString_String_ReturnsValue() =>
        Assert.Equal("abc", Converters.NumberToString(Parse("\"abc\"")));

    [Theory]
    [InlineData("fc")]
    [InlineData("asrs")]
    [InlineData("all")]
    public void WarehouseCodeAndLabel_RoundTrip(string code) =>
        Assert.Equal(code, Converters.WarehouseCodeFromLabel(Converters.WarehouseLabelFromCode(code)));

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}