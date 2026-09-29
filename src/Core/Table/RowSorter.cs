// 创建者: PlatyPus
// 创建时间: 2026-09-29
// 作用: 表格列排序（纯逻辑，零 UI 依赖）：数值列按数值比较、异常种类按严重度比较、其余按文本序比较；
//       排序键下标对齐 TableColumns 列序，供主面板点击表头排序复用并可脱离界面单测。

using System.Globalization;
using StockDiff.Core.Convert;
using StockDiff.Core.Models;

namespace StockDiff.Core.Table;

// 排序方向
public enum SortDirection { Ascending, Descending }

public static class RowSorter
{
    // 数值列下标（仓库数量 / WMS数量 / 差异数量）：必须按数值比较，避免文本序出现 9 > 100
    private static readonly int[] NumericColumns = { 3, 4, 5 };

    // 异常种类列下标：按严重度排序（数量差异 → 储位/冻结 → 效期/其他 → 无差异）
    private const int KindColumn = 1;

    // 按列排序：返回新列表（OrderBy 为稳定排序，同键保持原有相对顺序）；columnIndex 越界时按原序返回
    public static List<StockDiffRow> Sort(IEnumerable<StockDiffRow>? rows, int columnIndex, SortDirection direction)
    {
        var source = rows ?? Enumerable.Empty<StockDiffRow>();
        if (columnIndex < 0 || columnIndex >= TableColumns.Columns.Count)
        {
            return source.ToList();
        }

        var comparer = ComparerFor(columnIndex);
        return (direction == SortDirection.Ascending
            ? source.OrderBy(row => row, comparer)
            : source.OrderByDescending(row => row, comparer)).ToList();
    }

    // 首次点击该列的默认方向：数值列取降序（差异大的在前，最符合排查诉求），其余取升序
    public static SortDirection DefaultDirection(int columnIndex) =>
        Array.IndexOf(NumericColumns, columnIndex) >= 0 ? SortDirection.Descending : SortDirection.Ascending;

    // 按列选取比较器：数值列 → 数值比较；异常种类列 → 严重度比较；其余（含 yyyyMMdd 效期文本）→ 序数文本比较
    private static IComparer<StockDiffRow> ComparerFor(int columnIndex)
    {
        if (Array.IndexOf(NumericColumns, columnIndex) >= 0)
        {
            return Comparer<StockDiffRow>.Create((left, right) => CompareQuantity(left, right, columnIndex));
        }

        if (columnIndex == KindColumn)
        {
            return Comparer<StockDiffRow>.Create((left, right) => SeverityRank(left).CompareTo(SeverityRank(right)));
        }

        // 效期列为 yyyyMMdd 定宽文本，序数比较即时间先后，无需额外解析
        return Comparer<StockDiffRow>.Create((left, right) => string.CompareOrdinal(
            TableColumns.CellValue(left!, columnIndex),
            TableColumns.CellValue(right!, columnIndex)));
    }

    // 数值比较：解析失败或空值视为最小（升序排在前面）
    private static int CompareQuantity(StockDiffRow? left, StockDiffRow? right, int columnIndex)
    {
        var a = QuantityKey(left, columnIndex);
        var b = QuantityKey(right, columnIndex);

        if (a is null && b is null)
        {
            return 0;
        }

        if (a is null)
        {
            return -1;
        }

        if (b is null)
        {
            return 1;
        }

        return a.Value.CompareTo(b.Value);
    }

    // 取数值列的数值键：以 decimal 解析，保留大数精度（不使用 double）
    private static decimal? QuantityKey(StockDiffRow? row, int columnIndex)
    {
        if (row is null)
        {
            return null;
        }

        var text = columnIndex switch
        {
            3 => Converters.NumberToString(row.WarehouseQty),
            4 => Converters.NumberToString(row.WmsQty),
            5 => Converters.NumberToString(row.QtyDiff),
            _ => ""
        };

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    // 严重度排序权重：数量差异最前，无差异最后
    private static int SeverityRank(StockDiffRow? row) =>
        DiffClassifier.Severity(DiffClassifier.Classify(row)) switch
        {
            DiffSeverity.Critical => 0,
            DiffSeverity.Warning => 1,
            DiffSeverity.Notice => 2,
            _ => 3
        };
}
