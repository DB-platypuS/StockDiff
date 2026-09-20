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

    public static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ConnectTestTimeout = TimeSpan.FromSeconds(5);

    public static string Version =>
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "0.0.0";

    public static string NormalizeBaseUrl(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? DefaultBaseUrl : raw.Trim();
}