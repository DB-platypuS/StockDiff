// 创建者: PlatyPus
// 创建时间: 2026-09-23
// 作用: DataGridView 视图层样式扩展：一次性外观（字体/行高/表头/网格线/双缓冲/选择样式）、逐格差异
//       格式化（行分级底色、悬停、成对不一致单元格、差异数量与异常种类强调）与选中行左侧竖条绘制。
//       视图事件只把参数转交给这些方法，样式逻辑不散落在事件处理器中。

using System.Reflection;
using StockDiff.Core.Models;
using StockDiff.Core.Table;

namespace StockDiff.App.Views;

// 行级渲染元数据：填充期按行预算一次，CellFormatting 只做 O(1) 查表
internal readonly record struct GridRowMeta(DiffKind Kind, DiffSeverity Severity, bool IsDiff, int MismatchMask);

internal static class GridStyler
{
    // 需右对齐的数字列（仓库数量/WMS数量/差异数量）：视图层对齐，不改动 Core 列定义
    private const int WarehouseQtyColumn = 3;
    private const int WmsQtyColumn = 4;
    private const int DiffQtyColumn = 5;
    private const int DiffKindColumn = 1;

    // 选中行左侧竖条宽度
    private const int SelectionBarWidth = 3;

    // 双缓冲属性（受保护）：反射开启避免自绘闪烁，不引入自定义子类
    private static readonly PropertyInfo? DoubleBufferedProperty =
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

    // 由记录构建行元数据（分类 + 严重度 + 是否差异 + 成对不一致掩码）
    internal static GridRowMeta BuildMeta(StockDiffRow? row)
    {
        var kind = DiffClassifier.Classify(row);
        return new GridRowMeta(kind, DiffClassifier.Severity(kind), DiffClassifier.IsDiff(row), DiffClassifier.MismatchMask(row));
    }

    // 一次性外观：字体、行高、表头、网格线、选择样式与双缓冲
    internal static void ApplyGridLook(this DataGridView grid)
    {
        DoubleBufferedProperty?.SetValue(grid, true);

        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = GridTheme.GridLine;
        grid.EnableHeadersVisualStyles = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.RowTemplate.Height = 30;

        var cell = grid.DefaultCellStyle;
        cell.Font = Theme.GridBodyFont;
        cell.ForeColor = Theme.Ink;
        cell.BackColor = GridTheme.RowNormal;
        cell.SelectionBackColor = GridTheme.SelectionBg;
        cell.SelectionForeColor = Theme.Ink;
        cell.Padding = new Padding(6, 0, 6, 0);

        var header = grid.ColumnHeadersDefaultCellStyle;
        header.Font = Theme.GridHeaderFont;
        header.BackColor = GridTheme.HeaderBg;
        header.ForeColor = Theme.Ink;
        header.SelectionBackColor = GridTheme.HeaderBg;
        header.SelectionForeColor = Theme.Ink;
        header.Padding = new Padding(6, 4, 6, 4);
    }

    // 逐格格式化：行分级底色 → 悬停 → 成对不一致单元格 → 差异数量/异常种类强调 → 数字列右对齐
    internal static void FormatCell(this DataGridView grid, DataGridViewCellFormattingEventArgs e, int hoverRow)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        var style = e.CellStyle;
        var meta = grid.Rows[e.RowIndex].Tag is GridRowMeta m ? m : default;
        var col = e.ColumnIndex;

        style.Font = Theme.GridBodyFont;
        style.ForeColor = Theme.Ink;

        // 1) 行底色：差异分级 > 悬停 > 斑马
        style.BackColor = meta.Severity switch
        {
            DiffSeverity.Critical => GridTheme.RowCritical,
            DiffSeverity.Warning => GridTheme.RowWarning,
            DiffSeverity.Notice => GridTheme.RowNotice,
            _ => e.RowIndex % 2 == 1 ? GridTheme.RowZebra : GridTheme.RowNormal
        };
        if (e.RowIndex == hoverRow)
        {
            style.BackColor = GridTheme.Hover;
        }

        // 2) 成对不一致单元格：两来源列同时强调（浅红底 + 深红粗体）
        var pairIndex = DiffClassifier.PairIndexForColumn(col);
        if (pairIndex >= 0 && (meta.MismatchMask & (1 << pairIndex)) != 0)
        {
            style.BackColor = GridTheme.CellMismatchBg;
            style.ForeColor = GridTheme.CellMismatchInk;
            style.Font = Theme.GridHeaderFont;
        }

        // 3) 差异数量列：等宽加粗、红色，成为全表最显眼的数字
        if (col == DiffQtyColumn && meta.IsDiff)
        {
            style.Font = Theme.GridMonoFont;
            style.ForeColor = GridTheme.DiffQtyInk;
        }

        // 4) 异常种类列：按分类着色
        if (col == DiffKindColumn)
        {
            style.ForeColor = KindInk(meta.Kind);
            if (meta.Kind != DiffKind.None)
            {
                style.Font = Theme.GridHeaderFont;
            }
        }

        // 5) 数字列右对齐（数量与差异数量）
        if (col is WarehouseQtyColumn or WmsQtyColumn or DiffQtyColumn)
        {
            style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        // 仅调整样式，不改写显示文本
        e.FormattingApplied = false;
    }

    // 选中行左侧主题色竖条（非绑定表格无 DataSource，RowPostPaint 可用）
    internal static void PaintRowChrome(this DataGridView grid, DataGridViewRowPostPaintEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count || !grid.Rows[e.RowIndex].Selected)
        {
            return;
        }

        using var brush = new SolidBrush(GridTheme.SelectionBar);
        e.Graphics.FillRectangle(brush, new Rectangle(0, e.RowBounds.Top, SelectionBarWidth, e.RowBounds.Height));
    }

    // 异常种类文字色
    private static Color KindInk(DiffKind kind) => kind switch
    {
        DiffKind.Quantity => GridTheme.KindQuantityInk,
        DiffKind.Location => GridTheme.KindLocationInk,
        DiffKind.Hold => GridTheme.KindHoldInk,
        DiffKind.Expiry => GridTheme.KindExpiryInk,
        DiffKind.Other => GridTheme.KindOtherInk,
        _ => Theme.Ink
    };
}
