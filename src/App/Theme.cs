// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 应用统一视觉主题，集中定义配色、字体、按钮样式、输入框描边与圆角图形，
//       供登录页与设置对话框复用，避免样式在各视图间重复定义。

using System.Drawing.Drawing2D;

namespace StockDiff.App;

internal static class Theme
{
    public static readonly Color Canvas = Color.FromArgb(244, 245, 247);
    public static readonly Color Accent = Color.FromArgb(47, 111, 237);
    public static readonly Color AccentHover = Color.FromArgb(37, 89, 199);
    public static readonly Color Ink = Color.FromArgb(31, 35, 41);
    public static readonly Color Muted = Color.FromArgb(138, 144, 153);
    public static readonly Color Subtle = Color.FromArgb(70, 76, 84);
    public static readonly Color Line = Color.FromArgb(227, 230, 235);
    public static readonly Color Error = Color.FromArgb(214, 69, 69);
    public static readonly Color Success = Color.FromArgb(38, 154, 92);
    public static readonly Color SecondaryHover = Color.FromArgb(246, 247, 249);

    // 字体按用途命名：页面标题 17 / 对话框标题 12 / 登录主按钮 10 / 对话框主按钮 9.5
    public static readonly Font AddressFont = new("Microsoft YaHei UI", 8.5F);
    public static readonly Font BodyFont = new("Microsoft YaHei UI", 9F);
    public static readonly Font PageTitleFont = new("Microsoft YaHei UI", 17F, FontStyle.Bold);
    public static readonly Font DialogTitleFont = new("Microsoft YaHei UI", 12F, FontStyle.Bold);
    public static readonly Font LoginButtonFont = new("Microsoft YaHei UI", 10F, FontStyle.Bold);
    public static readonly Font DialogButtonFont = new("Microsoft YaHei UI", 9.5F, FontStyle.Bold);

    // 表格字体：表头粗体、内容常规、关键数字等宽（Consolas 数字等宽，便于纵向核对）
    public static readonly Font GridHeaderFont = new("Microsoft YaHei UI", 9F, FontStyle.Bold);
    public static readonly Font GridBodyFont = new("Microsoft YaHei UI", 9F);
    public static readonly Font GridMonoFont = new("Consolas", 10F, FontStyle.Bold);

    // 统计摘要字体：卡片小标题常规、数值大号粗体
    public static readonly Font StatCaptionFont = new("Microsoft YaHei UI", 9F);
    public static readonly Font StatValueFont = new("Microsoft YaHei UI", 16F, FontStyle.Bold);

    // 筛选栏字体：工具栏标签与下拉框，加大字号便于阅读（下拉高度随字号增大）
    public static readonly Font FilterFont = new("Microsoft YaHei UI", 11F);

    // 主按钮样式：蓝色实底、无边框、悬停加深
    public static void StylePrimary(Button button, Font font)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentHover;
        button.FlatAppearance.MouseDownBackColor = AccentHover;
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.Font = font;
        button.Cursor = Cursors.Hand;
    }

    // 次要按钮样式：白底细边框、悬停浅灰；wideHitArea 为真时追加左右内边距（登录页两个按钮用）
    public static void StyleSecondary(Button button, Font font, bool wideHitArea = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Line;
        button.FlatAppearance.MouseOverBackColor = SecondaryHover;
        button.BackColor = Color.White;
        button.ForeColor = Subtle;
        button.Font = font;
        button.Cursor = Cursors.Hand;

        if (wideHitArea)
        {
            button.Padding = new Padding(14, 0, 14, 0);
            button.TextAlign = ContentAlignment.MiddleCenter;
        }
    }

    // 用圆角描边容器包裹无边框输入框，innerPadding 为容器内边距；尺寸由调用方设置
    public static Panel WrapInput(TextBox box, Padding innerPadding)
    {
        var wrap = new Panel
        {
            BackColor = Color.White,
            Padding = innerPadding
        };
        wrap.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Line);
            using var path = RoundRect(new Rectangle(0, 0, wrap.Width - 1, wrap.Height - 1), 6);
            e.Graphics.DrawPath(pen, path);
        };
        wrap.Controls.Add(box);
        return wrap;
    }

    // 统一输入框外框工厂：以相同内边距包装无边框输入框，并指定外框尺寸与边距
    public static Panel MakeInput(TextBox box, Padding innerPadding, Size size, Padding margin)
    {
        var wrap = WrapInput(box, innerPadding);
        wrap.Size = size;
        wrap.Margin = margin;
        return wrap;
    }

    // 构造圆角矩形路径，供卡片与输入框的圆角绘制复用
    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
