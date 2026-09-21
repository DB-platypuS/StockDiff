
// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 接口地址设置收口，校验并归一化地址、落盘持久化、更新客户端并清空令牌。

using StockDiff.Core.Api;

namespace StockDiff.Core.Config;

// 持久化存储抽象：由 App 层实现（落盘 settings.json），Core 只依赖抽象，便于单测
public interface IBaseUrlStore
{
    string? Load();                  // 回读已持久化地址；无值返回 null
    bool Save(string baseUrl);       // 持久化地址；返回 false 表示写入失败（内存值仍生效，由调用方提示用户）
}

// 地址设置唯一入口（收口方法）：登录 / 连接测试 / 设置对话框三处统一走它
public static class BaseUrlSetter
{
    // 校验并归一化：空白→默认地址；否则须 http/https 绝对地址且含主机名
    public static bool TryNormalize(string? raw, out string normalized, out string error)
    {
        normalized = AppConfig.NormalizeBaseUrl(raw);
        error = "";

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "接口地址格式不正确，请输入形如 http://主机:端口 的地址";
            return false;
        }

        return true;
    }

    // 解析启动地址优先级：本地持久化（用户显式设置）> 环境变量（仅作首次默认值）
    // 两者均无有效值时返回 null，由调用方兜底默认地址
    public static string? ResolveStartupBaseUrl(string? persisted, string? fromEnvironment)
    {
        if (!string.IsNullOrWhiteSpace(persisted))
        {
            return persisted.Trim();
        }

        return string.IsNullOrWhiteSpace(fromEnvironment) ? null : fromEnvironment.Trim();
    }

    // 便捷重载：不关心落盘结果时使用；需向用户提示落盘失败请用带 out 的重载
    public static string SetBaseUrl(ApiClient client, IBaseUrlStore store, string? raw)
        => SetBaseUrl(client, store, raw, out _);

    // 校验 → 落盘 → 更新 client.BaseUrl → ClearToken；返回归一化后的地址
    // persisted：true=已成功持久化；false=内存中已生效但未写入本地配置
    // client / store 为空：抛 ArgumentNullException；地址非法：抛 ArgumentException（消息可直接展示）
    public static string SetBaseUrl(ApiClient client, IBaseUrlStore store, string? raw, out bool persisted)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(store);

        if (!TryNormalize(raw, out var normalized, out var error))
        {
            throw new ArgumentException(error, nameof(raw));
        }

        persisted = store.Save(normalized);
        client.BaseUrl = normalized;
        client.ClearToken();
        return normalized;
    }
}
