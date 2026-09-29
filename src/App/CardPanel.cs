// 创建者: PlatyPus
// 创建时间: 2026-09-29
// 作用: 自绘圆角卡片面板（白底 + 浅灰圆角描边），供登录页与主面板复用，避免圆角绘制逻辑在多处重复。

using System.Drawing.Drawing2D;

namespace StockDiff.App;

// 自绘圆角卡片面板：开启双层缓冲避免闪烁，尺寸退化时跳过 Region 与描边
internal sealed class CardPanel : Panel
{
    // 圆角半径：Region 裁剪与描边共用同一值，保证边缘吻合
    private const int CornerRadius = 12;

    public CardPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }

    // 尺寸变化时重建圆角 Region，使卡片边缘保持圆角
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width <= 2 || Height <= 2)
        {
            return;
        }

        var previous = Region;
        using var path = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        Region = new Region(path);
        previous?.Dispose();
    }

    // 先填充背景色，再绘制浅灰圆角描边
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (Width <= 2 || Height <= 2)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        using var pen = new Pen(Theme.Line);
        e.Graphics.DrawPath(pen, path);
    }
}
