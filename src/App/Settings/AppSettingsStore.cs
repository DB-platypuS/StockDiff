// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: IBaseUrlStore 的 App 层适配器，桥接到静态 AppSettings，供 Core 的地址设置收口使用。

using StockDiff.Core.Config;

namespace StockDiff.App.Settings;

public sealed class AppSettingsStore : IBaseUrlStore
{
    // 回读地址：AppSettings.BaseUrl 已做空值兜底，永不返回 null
    public string? Load() => AppSettings.BaseUrl;

    // 落盘地址：走 AppSettings 的归一化与原子写，并把写入结果原样上报给 Core
    public bool Save(string baseUrl) => AppSettings.TrySetBaseUrl(baseUrl);
}
