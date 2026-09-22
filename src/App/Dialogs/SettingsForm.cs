// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 接口地址设置对话框，预填当前地址，确认后统一走 BaseUrlSetter 校验、落盘并清空令牌；取消不产生任何副作用。

using System.Diagnostics;
using StockDiff.Core.Api;
using StockDiff.Core.Config;

namespace StockDiff.App.Dialogs;

public sealed class SettingsForm : Form
{
    // 表单尺寸与控件坐标常量，集中定义避免散落魔数
    private const int Edge = 24;
    private const int InputWidth = 372;
    private const int ButtonWidth = 88;
    private const int ButtonHeight = 32;

    private readonly ApiClient _client;
    private readonly IBaseUrlStore _store;

    private readonly TextBox _urlBox = new() { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill };
    private readonly Label _statusLabel = new() { AutoSize = true, ForeColor = Theme.Muted, Font = Theme.BodyFont };
    private readonly Button _okButton = new() { Text = "保存", Size = new Size(ButtonWidth, ButtonHeight) };
    private readonly Button _cancelButton = new() { Text = "取消", Size = new Size(ButtonWidth, ButtonHeight) };

    // 保存成功标记：调用方据此刷新地址展示并提示重新登录
    public bool Saved { get; private set; }

    // 构造对话框：预填当前地址 → 组装固定尺寸表单 → 绑定按钮事件
    public SettingsForm(ApiClient client, IBaseUrlStore store)
    {
        _client = client;
        _store = store;

        Text = "接口地址设置";
        Font = Theme.BodyFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.White;
        ClientSize = new Size(420, 208);

        _urlBox.Text = client.BaseUrl;
        _urlBox.Font = Theme.BodyFont;

        BuildLayout();

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        _okButton.Click += OnSaveClick;
        _cancelButton.Click += OnCancelClick;
        _urlBox.TextChanged += (_, _) => SetStatus("");
    }

    // 固定坐标布局：标题 / 说明 / 地址输入框 / 状态行 / 右下角按钮
    private void BuildLayout()
    {
        var title = new Label { Text = "接口地址", Font = Theme.DialogTitleFont, ForeColor = Theme.Ink, AutoSize = true, Location = new Point(Edge, 20) };
        var hint = new Label { Text = "修改接口地址后需要重新登录。", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(Edge, 50) };
        var input = MakeInput(_urlBox, new Point(Edge, 78), new Size(InputWidth, 36));

        _statusLabel.Location = new Point(Edge, 122);
        Theme.StylePrimary(_okButton, Theme.DialogButtonFont);
        Theme.StyleSecondary(_cancelButton, Theme.BodyFont);
        _okButton.Location = new Point(210, 152);
        _cancelButton.Location = new Point(308, 152);

        Controls.Add(title);
        Controls.Add(hint);
        Controls.Add(input);
        Controls.Add(_statusLabel);
        Controls.Add(_okButton);
        Controls.Add(_cancelButton);
    }

    // 圆角描边容器包裹无边框输入框，与登录页输入框外观保持一致（共享 Theme.MakeInput 工厂）
    private static Panel MakeInput(TextBox box, Point location, Size size)
    {
        var wrap = Theme.MakeInput(box, new Padding(10, 8, 10, 8), size, Padding.Empty);
        wrap.Location = location;
        return wrap;
    }

    // 保存：统一走 BaseUrlSetter（校验 → 落盘 → 更新客户端 → 清空令牌）
    // 非法地址就地红字提示且不关闭对话框；其它异常记日志并弹窗，保持统一错误出口
    private void OnSaveClick(object? sender, EventArgs e)
    {
        try
        {
            _urlBox.Text = BaseUrlSetter.SetBaseUrl(_client, _store, _urlBox.Text, out var persisted);
            Saved = true;

            if (!persisted)
            {
                Trace.WriteLine("[设置] 接口地址已生效，但未能写入本地配置文件");
                MessageBox.Show(this, "接口地址已生效，但未能保存到本地配置文件，重启后将恢复原地址。",
                    "保存提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            DialogResult = DialogResult.OK;
        }
        catch (ArgumentException ex)
        {
            SetStatus(ex.Message, Theme.Error);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[设置] 保存接口地址失败: {ex}");
            SetStatus("保存失败", Theme.Error);
            MessageBox.Show(this, ex.Message, "接口地址设置失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // 取消：不落盘、不清令牌，Saved 保持 false
    private void OnCancelClick(object? sender, EventArgs e)
    {
        Saved = false;
        DialogResult = DialogResult.Cancel;
    }

    // 状态行文案与颜色，具体实现收口在 UiHelper
    private void SetStatus(string text, Color? color = null) =>
        UiHelper.SetStatus(_statusLabel, text, color);
}
