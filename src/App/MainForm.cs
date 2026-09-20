// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 应用外壳窗体，承载并切换登录页与主面板两个视图，标题显示应用名与版本号。

using StockDiff.Core.Config;

namespace StockDiff.App;

public sealed class MainForm : Form
{
    private readonly Panel _content = new() { Dock = DockStyle.Fill };

    public MainForm()
    {
        Text = $"{AppConfig.AppName} v{AppConfig.Version}";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1100, 700);
        MinimumSize = new Size(900, 600);
        Controls.Add(_content);
    }
}
