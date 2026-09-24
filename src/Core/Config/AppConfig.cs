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

    // 编译期注入的本地私有默认地址所在的程序集元数据键（由未提交的 Directory.Build.local.props 提供）
    public const string LocalDefaultBaseUrlKey = "StockDiffDefaultBaseUrl";

    // 本地数据目录名：配置（settings.json）与日志（logs/）共用，避免字面量在 App 层多处重复
    public const string DataDirName = "kc-stock-diff";

    // 配置文件与日志目录名：均位于数据目录下，集中定义避免字面量在 App 层散落
    public const string SettingsFileName = "settings.json";
    public const string LogDirName = "logs";

    // 原子写临时文件后缀：先写 xxx.tmp 再原子替换，避免进程中断留下半截文件（配置与导出共用）
    public const string TempFileSuffix = ".tmp";

    // 日志文件名与单文件上限（2MB）：超出则轮转为 app.log.1，避免长期运行无限增长
    public const string LogFileName = "app.log";
    public const long LogMaxBytes = 2 * 1024 * 1024;

    // 各接口超时：登录 20s、拉取数据 60s、连接测试 5s
    public static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ConnectTestTimeout = TimeSpan.FromSeconds(5);

    // 版本号唯一来源：csproj 的 <Version>，去掉可能存在的 git 哈希后缀
    public static string Version =>
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "0.0.0";

    // 本地私有默认接口地址：取入口程序集元数据中由发布方注入的内网地址；
    // 未注入（他人克隆源码 / 跑单测 / 未配置本地文件）时返回 null，由调用方回退 DefaultBaseUrl
    public static string? LocalDefaultBaseUrl =>
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == LocalDefaultBaseUrlKey)?.Value is { Length: > 0 } raw
            ? raw.Trim()
            : null;

    // 归一化接口地址：去首尾空白，为空时兜底默认地址
    public static string NormalizeBaseUrl(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? DefaultBaseUrl : raw.Trim();
}