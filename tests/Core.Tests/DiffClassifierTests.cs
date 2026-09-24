// 创建者: PlatyPus
// 创建时间: 2026-09-23
// 作用: DiffClassifier 单元测试，锁定差异分类（关键词/兜底/空值）、严重度分级、成对列不一致判定
//       与列 → 成对列映射。

using System.Text.Json;
using StockDiff.Core.Models;
using StockDiff.Core.Table;
using Xunit;

namespace Core.Tests;

public sealed class DiffClassifierTests
{
    [Theory]
    [InlineData("数量不一致", DiffKind.Quantity)]
    [InlineData("储位不一致", DiffKind.Location)]
    [InlineData("WMS库位不一致", DiffKind.Location)]
    [InlineData("冻结状态不一致", DiffKind.Hold)]
    [InlineData("过期日期不一致", DiffKind.Expiry)]
    [InlineData("效期不一致", DiffKind.Expiry)]
    [InlineData("未知异常", DiffKind.Other)]
    public void Classify_Keyword_ReturnsExpectedKind(string diffType, DiffKind expected)
    {
        var row = new StockDiffRow { DiffType = diffType };
        Assert.Equal(expected, DiffClassifier.Classify(row));
    }

    [Fact]
    // diff_type 缺失但差异数量非零 → 兜底为数量差异
    public void Classify_EmptyTypeWithNonZeroQty_IsQuantity()
    {
        var row = new StockDiffRow { DiffType = "", QtyDiff = Number("-2") };
        Assert.Equal(DiffKind.Quantity, DiffClassifier.Classify(row));
    }

    [Fact]
    // diff_type 缺失且差值为 0，但储位两来源不同 → 兜底为储位差异
    public void Classify_EmptyTypeWithLocationMismatch_IsLocation()
    {
        var row = new StockDiffRow { Location = "A-01", WarehouseNo = "B-02" };
        Assert.Equal(DiffKind.Location, DiffClassifier.Classify(row));
    }

    [Fact]
    // 全空行 / null → 无差异
    public void Classify_AllEmpty_IsNone()
    {
        Assert.Equal(DiffKind.None, DiffClassifier.Classify(new StockDiffRow()));
        Assert.Equal(DiffKind.None, DiffClassifier.Classify(null));
    }

    [Fact]
    // 严重度分级
    public void Severity_MapsByKind()
    {
        Assert.Equal(DiffSeverity.Critical, DiffClassifier.Severity(DiffKind.Quantity));
        Assert.Equal(DiffSeverity.Warning, DiffClassifier.Severity(DiffKind.Location));
        Assert.Equal(DiffSeverity.Warning, DiffClassifier.Severity(DiffKind.Hold));
        Assert.Equal(DiffSeverity.Notice, DiffClassifier.Severity(DiffKind.Expiry));
        Assert.Equal(DiffSeverity.Normal, DiffClassifier.Severity(DiffKind.None));
    }

    [Fact]
    // 数量按数值比较：100 与 100.0 视为一致
    public void HasMismatch_QuantityIsNumeric()
    {
        var pair = DiffClassifier.Pairs[0];
        var row = new StockDiffRow { WarehouseQty = Number("100"), WmsQty = Number("100.0") };
        Assert.False(DiffClassifier.HasMismatch(row, pair));
    }

    [Fact]
    // 成对不一致掩码：储位不同 → 第 1 位（Pairs[1]）置位
    public void MismatchMask_SetsLocationBit()
    {
        var row = new StockDiffRow { Location = "A-01", WarehouseNo = "B-02" };
        Assert.Equal(1 << 1, DiffClassifier.MismatchMask(row));
    }

    [Fact]
    // 列 → 成对列映射：数量(3/4)、储位(6/7)、冻结(8/9)、效期(10/11)
    public void ColumnToPair_MapsExpectedColumns()
    {
        Assert.Equal(0, DiffClassifier.PairIndexForColumn(3));
        Assert.Equal(0, DiffClassifier.PairIndexForColumn(4));
        Assert.Equal(1, DiffClassifier.PairIndexForColumn(6));
        Assert.Equal(1, DiffClassifier.PairIndexForColumn(7));
        Assert.Equal(2, DiffClassifier.PairIndexForColumn(8));
        Assert.Equal(3, DiffClassifier.PairIndexForColumn(10));
        Assert.Equal(-1, DiffClassifier.PairIndexForColumn(0));
        Assert.Equal(-1, DiffClassifier.PairIndexForColumn(5));
    }

    // 构造 JsonElement 数字字面量
    private static JsonElement Number(string raw) => JsonDocument.Parse(raw).RootElement;
}
