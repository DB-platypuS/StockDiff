// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 全局配置与常量，提供默认接口地址、API 前缀、各请求超时与版本号唯一来源。

using System.Reflection;

namespace StockDiff.Core.Config;

public static class AppConfig
{
    public const string DefaultBaseUrl = "http://127.0.0.1:7880";
    public const string ApiPrefix = "/api/v1";
    public const string AppName = "库存差异比对系统";
    public const string BaseUrlEnvVar = "KC_STOCKDIFF_BASE_URL";

    // 各接口超时：登录 20s、拉取数据 60s、连接测试 5s
    public static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ConnectTestTimeout = TimeSpan.FromSeconds(5);

    // 版本号唯一来源：csproj 的 <Version>，去掉可能存在的 git 哈希后缀
    public static string Version =>
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "0.0.0";

    // 归一化接口地址：去首尾空白，为空时兜底默认地址
    public static string NormalizeBaseUrl(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? DefaultBaseUrl : raw.Trim();
}