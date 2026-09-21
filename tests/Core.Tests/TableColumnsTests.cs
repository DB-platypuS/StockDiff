// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: TableColumns 单元测试，锁定 12 列的名称、顺序、宽度与对齐规则，并校验取值与边界兜底。

using System.Text.Json;
using StockDiff.Core.Table;
using StockDiffRow = StockDiff.Core.Models.StockDiff;
using Xunit;

namespace Core.Tests;

public sealed class TableColumnsTests
{
    private static readonly string[] ExpectedHeaders =
    {
        "物料编码", "异常种类", "仓库类型", "仓库数量", "WMS数量", "差异数量",
        "储位", "WMS库位", "仓库冻结", "WMS冻结", "仓库效期", "WMS效期"
    };

    private static readonly int[] ExpectedWidths =
    {
        180, 120, 80, 90, 90, 90, 120, 120, 80, 80, 100, 100
    };

    [Fact]
    public void Columns_HasTwelveInOrder()
    {
        Assert.Equal(12, TableColumns.Columns.Count);
        Assert.Equal(ExpectedHeaders, TableColumns.Headers());
    }

    [Fact]
    public void Columns_WidthsMatchContract() =>
        Assert.Equal(ExpectedWidths, TableColumns.Columns.Select(c => c.Width).ToArray());

    [Fact]
    public void OnlyQtyDiffColumn_IsRightAligned()
    {
        var rightAligned = TableColumns.Columns
            .Select((c, i) => (c.Header, c.Align))
            .Where(x => x.Align != ColumnAlign.Left)
            .ToArray();

        var single = Assert.Single(rightAligned);
        Assert.Equal("差异数量", single.Header);
        Assert.Equal(ColumnAlign.Right, single.Align);
        Assert.Equal(ColumnAlign.Right, TableColumns.Align(5));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(12)]
    [InlineData(99)]
    public void Align_OutOfRange_FallsBackToLeft(int col) =>
        Assert.Equal(ColumnAlign.Left, TableColumns.Align(col));

    [Theory]
    [InlineData(0, "AC04672026013030856")]
    [InlineData(1, "")]
    [InlineData(2, "方仓")]
    [InlineData(3, "2640")]
    [InlineData(4, "2641")]
    [InlineData(5, "-1")]
    [InlineData(6, "3-21-1-2")]
    [InlineData(7, "")]
    [InlineData(8, "N")]
    [InlineData(9, "Y")]
    [InlineData(10, "20271124")]
    [InlineData(11, "20271124")]
    public void CellValue_ReturnsValuePerColumn(int col, string expected) =>
        Assert.Equal(expected, TableColumns.CellValue(Sample(), col));

    [Theory]
    [InlineData(-1)]
    [InlineData(12)]
    public void CellValue_OutOfRange_ReturnsEmpty(int col) =>
        Assert.Equal("", TableColumns.CellValue(Sample(), col));

    [Fact]
    public void CellValue_NullRow_ReturnsEmpty() =>
        Assert.Equal("", TableColumns.CellValue(null!, 0));

    private static StockDiffRow Sample()
    {
        var root = JsonDocument.Parse(
            """{"warehouse_qty":2640,"wms_qty":2641,"qty_diff":-1}""").RootElement;

        return new StockDiffRow
        {
            MaterialCode = "AC04672026013030856",
            WarehouseType = "fc",
            WarehouseQty = root.GetProperty("warehouse_qty").Clone(),
            WmsQty = root.GetProperty("wms_qty").Clone(),
            QtyDiff = root.GetProperty("qty_diff").Clone(),
            Location = "3-21-1-2",
            WarehouseHold = "N",
            WmsHold = "Y",
            WarehouseExpiry = "20271124",
            WmsExpiry = "20271124"
        };
    }
}