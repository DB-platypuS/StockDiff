// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 接口地址本地持久化，读写 %AppData%\kc-stock-diff\settings.json，采用写临时文件再替换的原子写。

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StockDiff.Core.Config;

namespace StockDiff.App.Settings;

public static class AppSettings
{
    private static readonly object Lock = new();
    private static string? _baseUrl;

    // 配置目录：%AppData%\kc-stock-diff（每用户可写，无需管理员权限）
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "kc-stock-diff");

    // 配置文件路径：settings.json
    private static string SettingsFile => Path.Combine(Dir, "settings.json");

    // 接口地址：读时归一化并兜底默认值；写时归一化后立即落盘
    public static string BaseUrl
    {
        get { lock (Lock) { return AppConfig.NormalizeBaseUrl(_baseUrl); } }
        set
        {
            lock (Lock)
            {
                _baseUrl = AppConfig.NormalizeBaseUrl(value);
                Save();
            }
        }
    }

    // 显式设置地址并落盘，返回是否成功持久化；失败时内存值仍生效，由调用方提示用户
    public static bool TrySetBaseUrl(string value)
    {
        lock (Lock)
        {
            _baseUrl = AppConfig.NormalizeBaseUrl(value);
            return Save();
        }
    }

    // 启动时载入一次地址配置
    public static void Load()
    {
        lock (Lock)
        {
            _baseUrl = ResolvePersistedOrEnv();
        }
    }

    // 解析启动地址：本地持久化（用户显式设置）优先于环境变量；均无值时返回 null，
    // 由 BaseUrl 的 getter 兜底默认地址。优先级规则统一由 Core 的 BaseUrlSetter 承载。
    private static string? ResolvePersistedOrEnv() =>
        BaseUrlSetter.ResolveStartupBaseUrl(
            ReadPersisted(), Environment.GetEnvironmentVariable(AppConfig.BaseUrlEnvVar));

    // 读取本地配置；文件不存在或内容损坏时返回 null 并记录日志
    private static string? ReadPersisted()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                return null;
            }

            var json = File.ReadAllText(SettingsFile, Encoding.UTF8);
            return JsonSerializer.Deserialize<SettingsData>(json)?.BaseUrl;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"[设置] 读取失败: {ex.Message}");
            return null;
        }
    }

    // 原子写：先写 settings.json.tmp，再用 File.Move 覆盖，避免进程被强杀时留下半截文件
    // 返回是否写入成功；失败时记录日志并返回 false，由调用方决定是否提示用户
    private static bool Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(new SettingsData { BaseUrl = AppConfig.NormalizeBaseUrl(_baseUrl) });
            var temp = SettingsFile + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            File.Move(temp, SettingsFile, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"[设置] 保存失败: {ex.Message}");
            return false;
        }
    }

    // 落盘结构：{"base_url": "..."}
    private sealed class SettingsData
    {
        [JsonPropertyName("base_url")] public string? BaseUrl { get; set; }
    }
}