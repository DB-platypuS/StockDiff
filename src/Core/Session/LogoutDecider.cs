// 创建者: PlatyPus
// 创建时间: 2026-09-23
// 作用: F8 会话管理 · 退出登录决策（纯逻辑，零 UI 依赖）：把「什么触发条件→是否退出→什么提示文案」
//       收口在 Core，DashboardView 的设置保存 / 手动退出 / 401 三处统一消费，避免文案与口径散落。

using StockDiff.Core.Api;

namespace StockDiff.Core.Session;

// 退出登录结果：ShouldLogout 为真时 UI 据此执行「取消在途请求 → 清空令牌 → 弹提示 → 跳登录页」
public sealed record LogoutOutcome(bool ShouldLogout, string Message);

// 退出登录决策：仅 UnauthorizedException 触发退出；设置保存与手动退出恒触发
public static class LogoutDecider
{
    // 文案常量：单一数据源，UI 与测试均引用此处，避免字面量在多分支重复
    public const string SettingsSavedMessage = "接口地址已更新，请重新登录。";
    public const string ManualLogoutMessage = "已退出登录，请重新登录";
    public const string DefaultExpiredMessage = "登录已过期，请重新登录";

    // 异常驱动的退出：仅 UnauthorizedException 触发，其余异常不退出（保持旧数据可导出）
    // UnauthorizedException.Message 为空/空白时兜底默认文案（构造默认值即此文案，此处防御显式传空）
    public static LogoutOutcome FromException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (ex is UnauthorizedException u)
        {
            return new LogoutOutcome(
                true,
                string.IsNullOrWhiteSpace(u.Message) ? DefaultExpiredMessage : u.Message);
        }

        return new LogoutOutcome(false, "");
    }

    // 设置保存后的退出：地址已变更并已清空令牌，必须重新登录
    public static LogoutOutcome FromSettingsSaved() => new(true, SettingsSavedMessage);

    // 手动点击「退出登录」按钮
    public static LogoutOutcome FromManualLogout() => new(true, ManualLogoutMessage);
}
