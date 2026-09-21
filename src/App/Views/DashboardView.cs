// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 主面板占位视图，登录成功后展示用户名与版本号，数据查询与表格由后续模块填充。

using StockDiff.Core.Config;

namespace StockDiff.App.Views;

public sealed class DashboardView : UserControl
{
    public DashboardView(string username)
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        var title = new Label
        {
            Text = $"欢迎，{username}",
            Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 24)
        };
        var hint = new Label
        {
            Text = "主界面建设中，数据查询与表格将在后续版本提供。",
            AutoSize = true,
            ForeColor = Color.Gray,
            Location = new Point(24, 64)
        };
        var version = new Label
        {
            Text = $"v{AppConfig.Version}",
            AutoSize = true,
            ForeColor = Color.Gray,
            Location = new Point(24, 92)
        };

        Controls.Add(title);
        Controls.Add(hint);
        Controls.Add(version);
    }
}