// 创建者: PlatyPus
// 创建时间: 2026-09-29
// 作用: RowSorter 单元测试，锁定列排序口径：数值列按数值（非文本序）、异常种类按严重度、效期按时间、稳定排序、
//       空值排前与越界列原序返回。

using System.Text.Json;
using StockDiff.Core.Models;
using StockDiff.Core.Table;
using Xunit;

namespace Core.Tests;

public sealed class RowSorterTests
{
    [Fact]
    // 数值列升序按数值比较：9 应排在 100 之前（文本序会得到相反结果）
    public void Sort_NumericColumn_OrdersByValueNotText()
    {
        var nine = new StockDiffRow { MaterialCode = "N09", QtyDiff = Number("9") };
        var hundred = new StockDiffRow { MaterialCode = "N100", QtyDiff = Number("100") };

        var sorted = RowSorter.Sort(new[] { hundred, nine }, 5, SortDirection.Ascending);

        Assert.Equal(new[] { "N09", "N100" }, Codes(sorted));
    }

    [Fact]
    // 数值列降序：差异大的在前
    public void Sort_NumericColumnDescending_LargestFirst()
    {
        var nine = new StockDiffRow { MaterialCode = "N09", QtyDiff = Number("9") };
        var hundred = new StockDiffRow { MaterialCode = "N100", QtyDiff = Number("100") };

        var sorted = RowSorter.Sort(new[] { nine, hundred }, 5, SortDirection.Descending);

        Assert.Equal(new[] { "N100", "N09" }, Codes(sorted));
    }

    [Fact]
    // 数值列空值视为最小：升序时排在最前
    public void Sort_NumericColumn_EmptyValueFirstAscending()
    {
        var withValue = new StockDiffRow { MaterialCode = "V", QtyDiff = Number("1") };
        var empty = new StockDiffRow { MaterialCode = "E" };

        var sorted = RowSorter.Sort(new[] { withValue, empty }, 5, SortDirection.Ascending);

        Assert.Equal(new[] { "E", "V" }, Codes(sorted));
    }

    [Fact]
    // 异常种类列按严重度：数量差异 → 储位差异 → 效期差异 → 无差异
    public void Sort_KindColumn_OrdersBySeverity()
    {
        var critical = new StockDiffRow { MaterialCode = "C", QtyDiff = Number("1") };
        var warning = new StockDiffRow { MaterialCode = "W", Location = "A", WarehouseNo = "B" };
        var notice = new StockDiffRow { MaterialCode = "N", DiffType = "效期不一致" };
        var normal = new StockDiffRow { MaterialCode = "Z" };

        var sorted = RowSorter.Sort(new[] { normal, notice, warning, critical }, 1, SortDirection.Ascending);

        Assert.Equal(new[] { "C", "W", "N", "Z" }, Codes(sorted));
    }

    [Fact]
    // 效期列为 yyyyMMdd 文本：升序即时间先后
    public void Sort_ExpiryColumn_OrdersChronologically()
    {
        var later = new StockDiffRow { MaterialCode = "L", WarehouseExpiry = "20270315" };
        var earlier = new StockDiffRow { MaterialCode = "E", WarehouseExpiry = "20260101" };

        var sorted = RowSorter.Sort(new[] { later, earlier }, 10, SortDirection.Ascending);

        Assert.Equal(new[] { "E", "L" }, Codes(sorted));
    }

    [Fact]
    // 文本列按序数：物料编码升序
    public void Sort_TextColumn_OrdersOrdinal()
    {
        var b = new StockDiffRow { MaterialCode = "B" };
        var a = new StockDiffRow { MaterialCode = "A" };
        var c = new StockDiffRow { MaterialCode = "C" };

        var sorted = RowSorter.Sort(new[] { b, a, c }, 0, SortDirection.Ascending);

        Assert.Equal(new[] { "A", "B", "C" }, Codes(sorted));
    }

    [Fact]
    // 稳定排序：同键保持原有相对顺序
    public void Sort_EqualKeys_PreservesOriginalOrder()
    {
        var first = new StockDiffRow { MaterialCode = "X1", QtyDiff = Number("5") };
        var second = new StockDiffRow { MaterialCode = "X2", QtyDiff = Number("5") };

        var sorted = RowSorter.Sort(new[] { first, second }, 5, SortDirection.Ascending);

        Assert.Equal(new[] { "X1", "X2" }, Codes(sorted));
    }

    [Fact]
    // 列下标越界：原序返回，且是新的列表实例
    public void Sort_ColumnOutOfRange_ReturnsSameOrderCopy()
    {
        var rows = new[] { new StockDiffRow { MaterialCode = "A" }, new StockDiffRow { MaterialCode = "B" } };

        var sorted = RowSorter.Sort(rows, 99, SortDirection.Ascending);

        Assert.Equal(new[] { "A", "B" }, Codes(sorted));
        Assert.NotSame(rows, sorted);
    }

    [Fact]
    // null 输入 → 空列表
    public void Sort_Null_ReturnsEmpty()
    {
        Assert.Empty(RowSorter.Sort(null, 0, SortDirection.Ascending));
    }

    [Theory]
    [InlineData(5, SortDirection.Descending)]
    [InlineData(3, SortDirection.Descending)]
    [InlineData(0, SortDirection.Ascending)]
    [InlineData(1, SortDirection.Ascending)]
    [InlineData(10, SortDirection.Ascending)]
    // 默认方向：数值列降序（差异大的在前），其余升序
    public void DefaultDirection_NumericDescendingOthersAscending(int columnIndex, SortDirection expected)
    {
        Assert.Equal(expected, RowSorter.DefaultDirection(columnIndex));
    }

    // 提取排序结果中的物料编码序列，便于断言顺序
    private static string[] Codes(List<StockDiffRow> rows) => rows.Select(row => row.MaterialCode).ToArray();

    // 构造数值 JsonElement 的测试辅助
    private static JsonElement Number(string raw) => JsonDocument.Parse(raw).RootElement;
}
