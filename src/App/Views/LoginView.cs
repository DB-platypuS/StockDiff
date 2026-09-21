// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 登录视图，展示接口地址并录入账号密码，登录成功后切换至主面板；失败给出提示并恢复按钮。

using System.Diagnostics;
using System.Drawing.Drawing2D;
using StockDiff.Core.Api;

namespace StockDiff.App.Views;

public sealed class LoginView : UserControl
{
    private static readonly Color Canvas = Color.FromArgb(244, 245, 247);
    private static readonly Color Accent = Color.FromArgb(47, 111, 237);
    private static readonly Color AccentHover = Color.FromArgb(37, 89, 199);
    private static readonly Color Ink = Color.FromArgb(31, 35, 41);
    private static readonly Color Muted = Color.FromArgb(138, 144, 153);
    private static readonly Color Subtle = Color.FromArgb(70, 76, 84);
    private static readonly Color Line = Color.FromArgb(227, 230, 235);
    private static readonly Color Error = Color.FromArgb(214, 69, 69);

    private static readonly Font AddressFont = new("Microsoft YaHei UI", 8.5F);
    private static readonly Font BodyFont = new("Microsoft YaHei UI", 9F);
    private static readonly Font TitleFont = new("Microsoft YaHei UI", 17F, FontStyle.Bold);
    private static readonly Font PrimaryButtonFont = new("Microsoft YaHei UI", 10F, FontStyle.Bold);

    private readonly MainForm _mainForm;
    private readonly ApiClient _client;

    private readonly Label _addressValue = new() { AutoSize = true, ForeColor = Muted };
    private readonly TextBox _userBox = new()
    {
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        PlaceholderText = "工号 / 账号"
    };
    private readonly TextBox _passwordBox = new()
    {
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        UseSystemPasswordChar = true,
        PlaceholderText = "密码"
    };
    private readonly Button _loginButton = new() { Text = "登 录", Height = 40, Dock = DockStyle.Top };
    private readonly Button _testButton = new() { Text = "测试连接", Size = new Size(88, 32), Margin = new Padding(0, 0, 10, 0) };
    private readonly Button _settingsButton = new() { Text = "API 设置", Size = new Size(88, 32), Margin = new Padding(0) };
    private readonly Label _statusLabel = new() { AutoSize = true, ForeColor = Muted };

    // 构建登录页：铺底色 → 卡片居中 → 绑定登录按钮与两个占位按钮事件
    public LoginView(MainForm mainForm, ApiClient client)
    {
        _mainForm = mainForm;
        _client = client;

        Dock = DockStyle.Fill;
        BackColor = Canvas;
        _addressValue.Text = $"接口地址    {client.BaseUrl}";
        _addressValue.Font = AddressFont;
        _statusLabel.Font = BodyFont;
        _loginButton.Margin = new Padding(0, 8, 0, 0);

        StylePrimary(_loginButton);
        StyleSecondary(_testButton);
        StyleSecondary(_settingsButton);

        var card = BuildCard();
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Canvas };
        host.Controls.Add(card);
        host.Resize += (_, _) => CenterCard(host, card);
        Controls.Add(host);
        CenterCard(host, card);

        _loginButton.Click += OnLoginClick;
        _testButton.Click += (_, _) => ShowComingSoon("测试连接");
        _settingsButton.Click += (_, _) => ShowComingSoon("API 设置");
        _userBox.TextChanged += (_, _) => SetStatus("");
        _passwordBox.TextChanged += (_, _) => SetStatus("");
    }

    // 组装卡片：标题区、分割线、接口地址、两个输入框、主按钮、次要按钮行与状态文字
    // 卡片尺寸由 inner.PreferredSize 显式计算，避开 AutoSize 与 Dock 的循环依赖
    private Control BuildCard()
    {
        var card = new CardPanel();

        var inner = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = 336,
            BackColor = Color.Transparent
        };

        _addressValue.Margin = new Padding(0, 0, 0, 18);
        _statusLabel.Margin = new Padding(0, 12, 0, 0);

        AddHeader(inner);
        inner.Controls.Add(CreateDivider());
        inner.Controls.Add(_addressValue);
        inner.Controls.Add(MakeInput(_userBox));
        inner.Controls.Add(MakeInput(_passwordBox));
        inner.Controls.Add(_loginButton);
        inner.Controls.Add(CreateActions());
        inner.Controls.Add(_statusLabel);

        inner.Location = new Point(32, 32);
        card.Controls.Add(inner);
        var preferred = inner.PreferredSize;
        card.Size = new Size(preferred.Width + 64, preferred.Height + 64);
        return card;
    }

    // 向卡片追加标题与副标题
    private static void AddHeader(TableLayoutPanel host)
    {
        host.Controls.Add(new Label
        {
            Text = "库存差异比对系统",
            Font = TitleFont,
            ForeColor = Ink,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        });

        host.Controls.Add(new Label
        {
            Text = "请使用工号与密码登录",
            Font = BodyFont,
            ForeColor = Muted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16)
        });
    }

    // 标题与表单之间的浅色分割线
    private static Panel CreateDivider() => new()
    {
        Height = 1,
        Width = 336,
        BackColor = Line,
        Margin = new Padding(0, 0, 0, 18)
    };

    // 「测试连接」「API 设置」两个次要按钮所在的横向容器
    private FlowLayoutPanel CreateActions()
    {
        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 14, 0, 0)
        };
        actions.Controls.Add(_testButton);
        actions.Controls.Add(_settingsButton);
        return actions;
    }

    // 用圆角边框容器包裹无边框输入框，自绘浅灰描边以获得现代输入框外观
    private static Panel MakeInput(TextBox box)
    {
        var wrap = new Panel
        {
            Height = 38,
            Width = 336,
            BackColor = Color.White,
            Padding = new Padding(10, 9, 10, 9),
            Margin = new Padding(0, 0, 0, 12)
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

    // 主按钮样式：蓝色实底、无边框、悬停加深，用于「登录」
    private static void StylePrimary(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentHover;
        button.FlatAppearance.MouseDownBackColor = AccentHover;
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.Font = PrimaryButtonFont;
        button.Cursor = Cursors.Hand;
    }

    // 次要按钮样式：白底细边框、悬停浅灰，用于「测试连接」「API 设置」
    private static void StyleSecondary(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Line;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(246, 247, 249);
        button.BackColor = Color.White;
        button.ForeColor = Subtle;
        button.Font = BodyFont;
        button.Padding = new Padding(14, 0, 14, 0);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Cursor = Cursors.Hand;
    }

    // F1 占位：提示该功能将在后续版本提供（真实逻辑分别属于 F3 / F2）
    private void ShowComingSoon(string feature)
    {
        SetStatus($"{feature}功能将在后续版本提供");
        MessageBox.Show(this, $"{feature}功能将在后续版本提供。", "功能开发中",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // 按宿主尺寸计算卡片左上角坐标，使卡片始终居中（窗口缩放时重算）
    private static void CenterCard(Control host, Control card)
    {
        card.Left = Math.Max(0, (host.ClientSize.Width - card.Width) / 2);
        card.Top = Math.Max(0, (host.ClientSize.Height - card.Height) / 2);
    }

    // 构造圆角矩形路径，供卡片与输入框的圆角绘制复用
    private static GraphicsPath RoundRect(Rectangle r, int radius)
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

    // 自绘圆角卡片面板：开启双层缓冲避免闪烁，尺寸退化时跳过 Region 与描边
    private sealed class CardPanel : Panel
    {
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
            using var path = RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 12);
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
            using var path = RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 12);
            using var pen = new Pen(Line);
            e.Graphics.DrawPath(pen, path);
        }
    }

    // 登录按钮处理：本地校验空输入 → 禁用控件防重入 → 调用 ApiClient 登录
    // 成功切换主面板；失败记录日志并用弹窗提示，最后恢复控件状态
    private async void OnLoginClick(object? sender, EventArgs e)
    {
        var username = _userBox.Text.Trim();
        var password = _passwordBox.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("请输入账号和密码", Error);
            return;
        }

        SetBusy(true);
        SetStatus("正在登录...");

        try
        {
            await _client.LoginAsync(username, password);
            _mainForm.ShowDashboard(username);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[登录] 失败: {ex}");
            SetStatus("登录失败", Error);
            MessageBox.Show(this, ex.Message, "登录失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // 登录期间禁用输入与按钮并切换等待光标，防止重复提交
    private void SetBusy(bool busy)
    {
        if (IsDisposed)
        {
            return;
        }

        _loginButton.Enabled = !busy;
        _testButton.Enabled = !busy;
        _settingsButton.Enabled = !busy;
        _userBox.Enabled = !busy;
        _passwordBox.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    // 更新状态文字与颜色；视图已销毁时直接返回，避免访问已释放控件
    private void SetStatus(string text, Color? color = null)
    {
        if (IsDisposed)
        {
            return;
        }

        _statusLabel.Text = text;
        _statusLabel.ForeColor = color ?? Muted;
    }
}