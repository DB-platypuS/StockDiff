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
    private readonly AppSettingsStore _settingsStore = new();

    // 构造外壳：设置标题与尺寸 → 加载持久化地址 → 创建唯一 ApiClient → 展示登录页
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

    // 全局唯一的 API 客户端，供各视图共享接口地址与登录令牌
    public ApiClient Client => _client;

    // 切换到登录页（未登录 / 退出登录 / 令牌过期时调用）
    public void ShowLogin() => ShowView(new LoginView(this, _client, _settingsStore));

    // 登录成功后切换到主面板，注入共享 ApiClient 与用户名、外壳与持久化存储，
    // 供数据查询刷新、F8 设置对话框与退出登录跳转登录页使用
    public void ShowDashboard(string username) =>
        ShowView(new DashboardView(_client, username, this, _settingsStore));

    // 视图切换：先释放旧视图，再装载新视图并铺满内容区
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
