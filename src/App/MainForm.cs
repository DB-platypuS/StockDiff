// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 应用外壳窗体，承载并切换登录页与主面板两个视图，标题显示应用名与版本号。

using StockDiff.App.Settings;
using StockDiff.App.Views;
using StockDiff.Core.Api;
using StockDiff.Core.Config;

namespace StockDiff.App;

public sealed class MainForm : Form
{
    private readonly Panel _content = new() { Dock = DockStyle.Fill };
    private readonly ApiClient _client;

    public MainForm()
    {
        Text = $"{AppConfig.AppName} v{AppConfig.Version}";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1100, 700);
        MinimumSize = new Size(900, 600);
        Controls.Add(_content);

        AppSettings.Load();
        _client = new ApiClient(AppSettings.BaseUrl);
        ShowLogin();
    }

    public ApiClient Client => _client;

    public void ShowLogin() => ShowView(new LoginView(this, _client));

    public void ShowDashboard(string username) => ShowView(new DashboardView(username));

    private void InitializeComponent()
    {
        SuspendLayout();
        // 
        // MainForm
        // 
        ClientSize = new Size(891, 422);
        Font = new Font("Microsoft YaHei UI", 4F);
        Name = "MainForm";
        ResumeLayout(false);

    }

    private void ShowView(UserControl view)
    {
        _content.SuspendLayout();
        var previous = _content.Controls.Cast<Control>().ToArray();
        _content.Controls.Clear();
        foreach (var control in previous)
        {
            control.Dispose();
        }

        view.Dock = DockStyle.Fill;
        _content.Controls.Add(view);
        _content.ResumeLayout(true);
    }
}
