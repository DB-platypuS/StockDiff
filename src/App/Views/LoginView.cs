// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 登录视图，展示接口地址并录入账号密码，登录成功后切换至主面板；失败给出提示并恢复按钮。

using System.Diagnostics;
using System.Drawing.Drawing2D;
using StockDiff.App.Dialogs;
using StockDiff.Core.Api;
using StockDiff.Core.Config;

namespace StockDiff.App.Views;

public sealed class LoginView : UserControl
{
    // 卡片与输入框宽度、内边距统一，避免多处硬编码
    private const int InputWidth = 336;
    private const int CardPadding = 32;

    private readonly MainForm _mainForm;
    private readonly ApiClient _client;
    private readonly IBaseUrlStore _store;

    private readonly Label _addressValue = new() { AutoSize = true, ForeColor = Theme.Muted };
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
        Margin = new Padding(0),
        UseSystemPasswordChar = true,
        PlaceholderText = "密码"
    };

    // 显示/隐藏密码切换：内嵌在密码框描边容器右侧的独立单元格中；自绘眼睛图标，不依赖字体字形粗细，
    // 用 2px 粗笔绘制保证清晰可见；放在 TableLayoutPanel 固定宽度列内，与密码框物理隔离不被遮挡
    private readonly Button _togglePasswordButton = new()
    {
        Dock = DockStyle.Fill,
        Margin = new Padding(0),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.White,
        Cursor = Cursors.Hand,
        TabStop = false
    };
    private readonly Button _loginButton = new() { Text = "登 录", Height = 40, Dock = DockStyle.Top };
    private readonly Button _testButton = new() { Text = "测试连接", Size = new Size(88, 32), Margin = new Padding(0, 0, 10, 0) };
    private readonly Button _settingsButton = new() { Text = "API 设置", Size = new Size(88, 32), Margin = new Padding(0) };
    private readonly Label _statusLabel = new() { AutoSize = true, ForeColor = Theme.Muted };

    // 登录请求取消源：视图被销毁（登录成功切主面板 / 关闭窗口）时取消在途请求
    private readonly CancellationTokenSource _loginCts = new();

    // 构建登录页：铺底色 → 卡片居中 → 绑定登录按钮与两个占位按钮事件
    public LoginView(MainForm mainForm, ApiClient client, IBaseUrlStore store)
    {
        _mainForm = mainForm;
        _client = client;
        _store = store;

        Dock = DockStyle.Fill;
        BackColor = Theme.Canvas;
        _addressValue.Text = $"接口地址    {client.BaseUrl}";
        _addressValue.Font = Theme.AddressFont;
        _statusLabel.Font = Theme.BodyFont;
        _loginButton.Margin = new Padding(0, 8, 0, 0);

        Theme.StylePrimary(_loginButton, Theme.LoginButtonFont);
        Theme.StyleSecondary(_testButton, Theme.BodyFont, wideHitArea: true);
        Theme.StyleSecondary(_settingsButton, Theme.BodyFont, wideHitArea: true);
        _togglePasswordButton.FlatAppearance.BorderSize = 0;
        _togglePasswordButton.FlatAppearance.MouseOverBackColor = Theme.SecondaryHover;
        _togglePasswordButton.Paint += OnTogglePasswordPaint;

        var card = BuildCard();
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
        host.Controls.Add(card);
        host.Resize += (_, _) => CenterCard(host, card);
        Controls.Add(host);
        CenterCard(host, card);

        _loginButton.Click += OnLoginClick;
        _testButton.Click += OnTestClick;
        _settingsButton.Click += OnSettingsClick;
        _togglePasswordButton.Click += (_, _) => TogglePassword();
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
            Width = InputWidth,
            BackColor = Color.Transparent
        };

        _addressValue.Margin = new Padding(0, 0, 0, 18);
        _statusLabel.Margin = new Padding(0, 12, 0, 0);

        AddHeader(inner);
        inner.Controls.Add(CreateDivider());
        inner.Controls.Add(_addressValue);
        inner.Controls.Add(MakeInput(_userBox));
        inner.Controls.Add(MakePasswordInput());
        inner.Controls.Add(_loginButton);
        inner.Controls.Add(CreateActions());
        inner.Controls.Add(_statusLabel);

        inner.Location = new Point(CardPadding, CardPadding);
        card.Controls.Add(inner);
        var preferred = inner.PreferredSize;
        card.Size = new Size(preferred.Width + CardPadding * 2, preferred.Height + CardPadding * 2);
        return card;
    }

    // 向卡片追加标题与副标题
    private static void AddHeader(TableLayoutPanel host)
    {
        host.Controls.Add(new Label
        {
            Text = "库存差异比对系统",
            Font = Theme.PageTitleFont,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        });

        host.Controls.Add(new Label
        {
            Text = "请使用工号与密码登录",
            Font = Theme.BodyFont,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16)
        });
    }

    // 标题与表单之间的浅色分割线
    private static Panel CreateDivider() => new()
    {
        Height = 1,
        Width = InputWidth,
        BackColor = Theme.Line,
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

    // 登录页输入框：统一宽度与下边距，圆角描边外观由 Theme 统一提供
    private static Panel MakeInput(TextBox box) =>
        Theme.MakeInput(box, new Padding(10, 9, 10, 9), new Size(InputWidth, 38), new Padding(0, 0, 0, 12));

    // 密码输入：用 TableLayoutPanel 把密码框（自适应列）与切换按钮（固定 38px 列）分到两个单元格，
    // 彻底避免 Dock=Fill 的文本框覆盖按钮；外层仍是带圆角描边的容器
    private Panel MakePasswordInput()
    {
        var wrap = new Panel
        {
            BackColor = Color.White,
            Padding = new Padding(10, 9, 8, 9),
            Size = new Size(InputWidth, 38),
            Margin = new Padding(0, 0, 0, 12)
        };
        wrap.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.Line);
            using var path = Theme.RoundRect(new Rectangle(0, 0, wrap.Width - 1, wrap.Height - 1), 6);
            e.Graphics.DrawPath(pen, path);
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38F));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        table.Controls.Add(_passwordBox, 0, 0);
        table.Controls.Add(_togglePasswordButton, 1, 0);
        wrap.Controls.Add(table);
        return wrap;
    }

    // 切换密码明文显示：只改变掩码方式，不清空已输入内容；重绘按钮切换眼睛图标（遮蔽→睁眼；明文→划掉）
    private void TogglePassword()
    {
        _passwordBox.UseSystemPasswordChar = !_passwordBox.UseSystemPasswordChar;
        _togglePasswordButton.Invalidate();
    }

    // 自绘密码切换按钮图标：用 2px 粗笔在按钮中心绘制眼睛（睁眼 = 椭圆框 + 瞳孔；划掉 = 再叠加一条斜线）
    private void OnTogglePasswordPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(_togglePasswordButton.BackColor);

        var bounds = _togglePasswordButton.ClientRectangle;
        var cx = bounds.Width / 2f;
        var cy = bounds.Height / 2f;
        var eyeW = 16f;
        var eyeH = 10f;

        using var pen = new Pen(Theme.Subtle, 2f);
        using var pupil = new SolidBrush(Theme.Subtle);

        // 眼睛外框：水平椭圆
        g.DrawEllipse(pen, cx - eyeW / 2, cy - eyeH / 2, eyeW, eyeH);
        // 瞳孔：中心实心圆
        g.FillEllipse(pupil, cx - 2.5f, cy - 2.5f, 5f, 5f);

        // 明文状态：叠加一条从左上到右下的斜线，表示"已显示 / 点击遮蔽"
        if (!_passwordBox.UseSystemPasswordChar)
        {
            var r = eyeW / 2 + 2;
            g.DrawLine(pen, cx - r, cy - r * 0.6f, cx + r, cy + r * 0.6f);
        }
    }

    // 「API 设置」：打开接口地址对话框；保存成功后刷新地址展示并提示重新登录
    // （地址已由 BaseUrlSetter 落盘并清空令牌，此处只负责界面反馈）
    private void OnSettingsClick(object? sender, EventArgs e)
    {
        using var dialog = new SettingsForm(_client, _store);
        dialog.ShowDialog(this);

        if (!dialog.Saved)
        {
            return;
        }

        _addressValue.Text = $"接口地址    {_client.BaseUrl}";
        SetStatus("接口地址已更新，请重新登录");
    }

    // 「测试连接」：对当前接口地址做 TCP 可达性探测，成功/失败两路分别提示（F3）
    private async void OnTestClick(object? sender, EventArgs e)
    {
        SetBusy(true);
        SetStatus("正在测试连接...");

        try
        {
            await _client.TestConnectionAsync(_loginCts.Token);
            SetStatus("✓ 连接成功", Theme.Success);
            MessageBox.Show(this, $"已成功连接到 {_client.BaseUrl}", "连接测试",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            // 视图已销毁导致的主动取消，属预期流程，不提示用户
            Trace.WriteLine("[连接测试] 请求已取消（视图已关闭）");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[连接测试] 失败: {ex}");
            SetStatus("连接失败", Theme.Error);
            MessageBox.Show(this, ex.Message, "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // 按宿主尺寸计算卡片左上角坐标，使卡片始终居中（窗口缩放时重算）
    private static void CenterCard(Control host, Control card)
    {
        card.Left = Math.Max(0, (host.ClientSize.Width - card.Width) / 2);
        card.Top = Math.Max(0, (host.ClientSize.Height - card.Height) / 2);
    }

    // 登录按钮处理：本地校验空输入 → 禁用控件防重入 → 调用 ApiClient 登录
    // 成功切换主面板；失败记录日志并用弹窗提示，最后恢复控件状态
    private async void OnLoginClick(object? sender, EventArgs e)
    {
        var username = _userBox.Text.Trim();
        var password = _passwordBox.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("请输入账号和密码", Theme.Error);
            return;
        }

        SetBusy(true);
        SetStatus("正在登录...");

        try
        {
            await _client.LoginAsync(username, password, _loginCts.Token);
            _mainForm.ShowDashboard(username);
        }
        catch (OperationCanceledException)
        {
            // 视图已销毁导致的主动取消，属预期流程，不提示用户
            Trace.WriteLine("[登录] 请求已取消（视图已关闭）");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[登录] 失败: {ex}");
            SetStatus("登录失败", Theme.Error);
            MessageBox.Show(this, ex.Message, "登录失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // 快捷键：回车直接登录（焦点在账号或密码框均可）；忙碌时忽略，避免重复提交
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter)
        {
            if (_loginButton.Enabled)
            {
                OnLoginClick(_loginButton, EventArgs.Empty);
            }

            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    // 销毁视图时取消在途登录请求；只取消不 Dispose，避免与在途请求的令牌注册产生释放竞态
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loginCts.Cancel();
        }

        base.Dispose(disposing);
    }

    // 登录期间禁用输入与按钮并切换等待光标，防止重复提交（实现收口在 UiHelper）
    private void SetBusy(bool busy) =>
        UiHelper.SetBusy(this, busy, this, _loginButton, _testButton, _settingsButton, _userBox, _passwordBox, _togglePasswordButton);

    // 更新状态文字与颜色，具体实现收口在 UiHelper
    private void SetStatus(string text, Color? color = null) =>
        UiHelper.SetStatus(_statusLabel, text, color);
}