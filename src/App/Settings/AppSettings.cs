// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 接口地址本地持久化，读写 %AppData%\kc-stock-diff\settings.json，采用写临时文件再替换的原子写。

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StockDiff.Core.Config;

namespace StockDiff.App.Settings;

public static class AppSettings
{
    private static readonly object Lock = new();
    private static string? _baseUrl;

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "kc-stock-diff");

    private static string SettingsFile => Path.Combine(Dir, "settings.json");

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

    public static void Load()
    {
        lock (Lock)
        {
            _baseUrl = ReadPersisted();
        }
    }

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
            System.Diagnostics.Debug.WriteLine($"读取设置失败: {ex.Message}");
            return null;
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(new SettingsData { BaseUrl = AppConfig.NormalizeBaseUrl(_baseUrl) });
            var temp = SettingsFile + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            File.Move(temp, SettingsFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
        }
    }

    private sealed class SettingsData
    {
        [JsonPropertyName("base_url")] public string? BaseUrl { get; set; }
    }
}