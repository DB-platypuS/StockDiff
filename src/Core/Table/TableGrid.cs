// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: F5 数据表格展示的纯渲染模型：由 TableColumns 派生表头与对齐方式，把行数据投影为
//       单元格文本矩阵，供 DashboardView 直接填充 DataGridView；零 UI 依赖，可脱离界面单测。

using StockDiff.Core.Models;

namespace StockDiff.Core.Table;

// 表格渲染模型：表头、单元格文本矩阵与每列对齐方式（不可变快照）
public sealed class TableGrid
{
    private readonly string[] _headers;
    private readonly string[][] _rows;
    private readonly ColumnAlign[] _aligns;

    private TableGrid(string[] headers, string[][] rows, ColumnAlign[] aligns)
    {
        _headers = headers;
        _rows = rows;
        _aligns = aligns;
    }

    // 表头文本（按列顺序），长度为 ColumnCount
    public IReadOnlyList<string> Headers => _headers;

    // 单元格文本矩阵，Rows[i][j] 为第 i 行第 j 列文本；长度与输入记录数一致
    public IReadOnlyList<string[]> Rows => _rows;

    // 每列对齐方式（按列顺序），派生自 TableColumns
    public IReadOnlyList<ColumnAlign> Aligns => _aligns;

    // 记录数
    public int RowCount => _rows.Length;

    // 列数（固定为 TableColumns 的列数，当前 12）
    public int ColumnCount => _headers.Length;

    // 是否为空数据（无记录）
    public bool IsEmpty => _rows.Length == 0;

    // 由记录集合构建表格模型：null / 空集合 → 仅含表头的空表格；
    // 集合内的 null 元素 → 该行全部单元格为空串，保持行数与记录数一致。
    public static TableGrid From(IEnumerable<StockDiffRow>? rows)
    {
        var source = rows?.ToList() ?? new List<StockDiffRow>();
        var cells = new string[source.Count][];

        for (var i = 0; i < source.Count; i++)
        {
            var line = new string[TableColumns.Columns.Count];
            for (var j = 0; j < line.Length; j++)
            {
                line[j] = TableColumns.CellValue(source[i], j);
            }

            cells[i] = line;
        }

        return new TableGrid(
            TableColumns.Headers(),
            cells,
            TableColumns.Columns.Select(c => c.Align).ToArray());
    }
}
