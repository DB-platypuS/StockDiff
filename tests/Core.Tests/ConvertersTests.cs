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
    // 仓库代码映射中文标签，未知或空一律回退「全部」
    public void WarehouseLabelFromCode_MapsAndFallsBack(string? code, string expected) =>
        Assert.Equal(expected, Converters.WarehouseLabelFromCode(code));

    [Theory]
    [InlineData("方仓", "fc")]
    [InlineData("立库", "asrs")]
    [InlineData("全部", "all")]
    [InlineData("未知", "all")]
    [InlineData("", "all")]
    [InlineData(null, "all")]
    // 中文标签反查仓库代码，未知或空一律回退 all
    public void WarehouseCodeFromLabel_MapsAndFallsBack(string? label, string expected) =>
        Assert.Equal(expected, Converters.WarehouseCodeFromLabel(label));

    [Fact]
    // JsonElement 默认值（Undefined）返回空串
    public void NumberToString_Undefined_ReturnsEmpty() =>
        Assert.Equal("", Converters.NumberToString(default(JsonElement)));

    [Fact]
    // JSON null 返回空串
    public void NumberToString_Null_ReturnsEmpty() =>
        Assert.Equal("", Converters.NumberToString(Parse("null")));

    [Theory]
    [InlineData("0")]
    [InlineData("2640")]
    [InlineData("-1")]
    // 整数直接取原始文本，不做数值转换
    public void NumberToString_Integer_KeepsRawText(string raw) =>
        Assert.Equal(raw, Converters.NumberToString(Parse(raw)));

    [Fact]
    // 超长整数保持精度，不得退化为科学计数或丢失末位
    public void NumberToString_LargeInteger_KeepsPrecision()
    {
        const string raw = "123456789012345678901234567890";
        Assert.Equal(raw, Converters.NumberToString(Parse(raw)));
    }

    [Fact]
    // 小数保留尾随零（12.50 不变成 12.5）
    public void NumberToString_Decimal_KeepsTrailingZeros() =>
        Assert.Equal("12.50", Converters.NumberToString(Parse("12.50")));

    [Fact]
    // 字符串类型的值原样返回
    public void NumberToString_String_ReturnsValue() =>
        Assert.Equal("abc", Converters.NumberToString(Parse("\"abc\"")));

    [Theory]
    [InlineData("fc")]
    [InlineData("asrs")]
    [InlineData("all")]
    // 代码与标签互转应能往返一致
    public void WarehouseCodeAndLabel_RoundTrip(string code) =>
        Assert.Equal(code, Converters.WarehouseCodeFromLabel(Converters.WarehouseLabelFromCode(code)));

    [Fact]
    public void UnknownCode_RoundTripsToAll() =>
        Assert.Equal("all", Converters.WarehouseCodeFromLabel(Converters.WarehouseLabelFromCode("xyz")));

    [Theory]
    [InlineData(" fc ")]
    [InlineData("asrs\t")]
    // 边界：代码仅做大小写归一、不做 Trim，带首尾空白视为未知值回退「全部」（UI 侧只传精确代码）
    public void WarehouseLabelFromCode_PaddedCode_FallsBackToAll(string code) =>
        Assert.Equal("全部", Converters.WarehouseLabelFromCode(code));

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void NumberToString_Bool_KeepsText(string raw)
    {
        var value = Converters.NumberToString(Parse(raw));
        Assert.Equal(raw, value, ignoreCase: true);
    }

    [Fact]
    public void NumberToString_Array_ReturnsRawJson() =>
        Assert.Equal("[1,2]", Converters.NumberToString(Parse("[1,2]")));

    [Fact]
    public void NumberToString_Object_ReturnsRawJson() =>
        Assert.Equal("""{"a":1}""", Converters.NumberToString(Parse("""{"a":1}""")));

    // 测试辅助：把 JSON 文本解析为独立的 JsonElement
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}