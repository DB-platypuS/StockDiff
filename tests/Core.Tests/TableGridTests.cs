// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: TableGrid 单元测试，锁定表格渲染模型的表头/列数/对齐/取值，并覆盖 null、空集合、
//       null 元素、缺字段与输入快照等边界。

using System.Text.Json;
using StockDiff.Core.Models;
using StockDiff.Core.Table;
using Xunit;

namespace Core.Tests;

public sealed class TableGridTests
{
    [Fact]
    // 正常流程：两条记录 → 12 列 2 行
    public void From_TwoRows_ProducesTwelveColumnsTwoRows()
    {
        var grid = TableGrid.From(new[] { FullRow(), FullRow() });

        Assert.Equal(12, grid.ColumnCount);
        Assert.Equal(2, grid.RowCount);
        Assert.False(grid.IsEmpty);
    }

    [Fact]
    // 表头与列定义单一数据源一致
    public void From_HeadersMatchTableColumns()
    {
        var grid = TableGrid.From(Array.Empty<StockDiffRow>());

        Assert.Equal(TableColumns.Headers(), grid.Headers.ToArray());
    }

    [Fact]
    // 对齐方式与列定义一致，且仅「差异数量」（第 5 列）右对齐
    public void From_AlignsMatchTableColumns()
    {
        var grid = TableGrid.From(Array.Empty<StockDiffRow>());

        Assert.Equal(12, grid.Aligns.Count);
        Assert.Equal(TableColumns.Columns.Select(c => c.Align).ToArray(), grid.Aligns.ToArray());
        Assert.Equal(ColumnAlign.Right, grid.Aligns[5]);
    }

    [Theory]
    [InlineData(0, "AC04672026013030856")]
    [InlineData(2, "方仓")]
    [InlineData(5, "-1")]
    [InlineData(11, "20271124")]
    // 单元格取值逐列与列定义一致
    public void From_CellValueMatchesColumn(int col, string expected)
    {
        var grid = TableGrid.From(new[] { FullRow() });

        Assert.Equal(expected, grid.Rows[0][col]);
    }

    [Fact]
    // 多行保持输入顺序
    public void From_PreservesInputOrder()
    {
        var first = FullRow();
        first.MaterialCode = "A-1";
        var second = FullRow();
        second.MaterialCode = "A-2";

        var grid = TableGrid.From(new[] { first, second });

        Assert.Equal("A-1", grid.Rows[0][0]);
        Assert.Equal("A-2", grid.Rows[1][0]);
    }

    [Fact]
    // 边界：null 入参 → 保留 12 列表头、0 行、IsEmpty
    public void From_Null_ReturnsHeaderOnlyEmptyGrid()
    {
        var grid = TableGrid.From(null);

        Assert.True(grid.IsEmpty);
        Assert.Equal(0, grid.RowCount);
        Assert.Equal(12, grid.ColumnCount);
    }

    [Fact]
    // 边界：空集合 → 同 null 表现
    public void From_EmptyCollection_ReturnsEmptyGrid()
    {
        var grid = TableGrid.From(Array.Empty<StockDiffRow>());

        Assert.True(grid.IsEmpty);
        Assert.Equal(0, grid.RowCount);
    }

    [Fact]
    // 边界：集合内 null 元素 → 该行全空，但行数不变
    public void From_NullElement_BecomesEmptyRow()
    {
        var grid = TableGrid.From(new List<StockDiffRow> { null!, FullRow() });

        Assert.Equal(2, grid.RowCount);
        Assert.All(grid.Rows[0], cell => Assert.Equal("", cell));
        Assert.Equal("AC04672026013030856", grid.Rows[1][0]);
    }

    [Fact]
    // 边界：数量字段缺失（JsonElement 默认值）→ 单元格为空串而非报错
    public void From_MissingQuantityFields_ReturnEmptyCells()
    {
        var grid = TableGrid.From(new[] { new StockDiffRow { MaterialCode = "M1" } });

        Assert.Equal("M1", grid.Rows[0][0]);
        Assert.Equal("", grid.Rows[0][3]);
        Assert.Equal("", grid.Rows[0][4]);
        Assert.Equal("", grid.Rows[0][5]);
    }

    [Fact]
    // Bug 回归：字符串字段在 JSON 中显式为 null 时，单元格文本为空串而非 null，
    // 避免 null 泄漏进 DataGridView 与后续 CSV 导出
    public void From_NullStringField_ReturnsEmptyCell()
    {
        var row = JsonSerializer.Deserialize<StockDiffRow>("""{"material_code":null}""")!;

        var grid = TableGrid.From(new[] { row });

        Assert.Equal("", grid.Rows[0][0]);
    }

    [Fact]
    // 防御性：构建后修改输入列表不影响已生成的快照
    public void From_DoesNotRetainInputList()
    {
        var input = new List<StockDiffRow> { FullRow() };
        var grid = TableGrid.From(input);

        input.Clear();

        Assert.Equal(1, grid.RowCount);
    }

    // 测试辅助：构造一条含三个数量字段的完整样本记录
    private static StockDiffRow FullRow()
    {
        var root = JsonDocument.Parse(
            """{"warehouse_qty":2640,"wms_qty":2641,"qty_diff":-1}""").RootElement;

        return new StockDiffRow
        {
            MaterialCode = "AC04672026013030856",
            DiffType = "数量不一致",
            WarehouseType = "fc",
            WarehouseQty = root.GetProperty("warehouse_qty").Clone(),
            WmsQty = root.GetProperty("wms_qty").Clone(),
            QtyDiff = root.GetProperty("qty_diff").Clone(),
            Location = "3-21-1-2",
            WarehouseNo = "B-01",
            WarehouseHold = "N",
            WmsHold = "Y",
            WarehouseExpiry = "20271124",
            WmsExpiry = "20271124"
        };
    }
}
