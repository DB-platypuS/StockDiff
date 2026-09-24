// 创建者: PlatyPus
// 创建时间: 2026-09-23
// 作用: 表格（DataGridView）专用色板：行正常/斑马/差异分级底色、成对不一致单元格色、差异数字与异常
//       种类文字色、悬停与选中色、网格与表头色。集中定义，避免颜色字面量散落在事件代码中。

namespace StockDiff.App;

internal static class GridTheme
{
    // 行底色（浅底深字，强色只给关键数字）
    public static readonly Color RowNormal = Color.White;
    public static readonly Color RowZebra = Color.FromArgb(247, 248, 250);
    public static readonly Color RowCritical = Color.FromArgb(253, 236, 236);
    public static readonly Color RowWarning = Color.FromArgb(255, 246, 229);
    public static readonly Color RowNotice = Color.FromArgb(255, 251, 230);
    public static readonly Color Hover = Color.FromArgb(234, 242, 254);

    // 选中：浅蓝底 + 主题色左竖条（浅底深字，不与差异红冲突）
    public static readonly Color SelectionBg = Color.FromArgb(219, 231, 254);
    public static readonly Color SelectionBar = Theme.Accent;

    // 成对不一致单元格（浅红底 + 深红粗体字）
    public static readonly Color CellMismatchBg = Color.FromArgb(253, 219, 219);
    public static readonly Color CellMismatchInk = Color.FromArgb(180, 35, 24);

    // 关键数字与异常种类文字色
    public static readonly Color DiffQtyInk = Theme.Error;
    public static readonly Color KindQuantityInk = Color.FromArgb(214, 69, 69);
    public static readonly Color KindLocationInk = Color.FromArgb(232, 130, 30);
    public static readonly Color KindHoldInk = Color.FromArgb(124, 92, 214);
    public static readonly Color KindExpiryInk = Color.FromArgb(176, 138, 20);
    public static readonly Color KindOtherInk = Theme.Subtle;

    // 网格与表头
    public static readonly Color GridLine = Theme.Line;
    public static readonly Color HeaderBg = Theme.SecondaryHover;
}
