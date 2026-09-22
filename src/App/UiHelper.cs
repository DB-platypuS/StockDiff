// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: 视图通用交互辅助（忙碌态切换、状态文字更新），消除登录页 / 主面板 / 对话框之间的重复实现，
//       并统一等待光标口径（此前登录页用 Cursor、主面板用 UseWaitCursor）。

namespace StockDiff.App;

internal static class UiHelper
{
    // 忙碌态切换：批量禁用/启用交互控件并切换等待光标；视图已释放时直接返回，避免访问已释放控件
    public static void SetBusy(Control owner, bool busy, Control cursorHost, params Control[] controls)
    {
        if (owner.IsDisposed)
        {
            return;
        }

        foreach (var control in controls)
        {
            control.Enabled = !busy;
        }

        cursorHost.UseWaitCursor = busy;
    }

    // 状态文字与颜色更新：未指定颜色时回退次要灰；标签已释放时跳过，避免访问已释放控件
    public static void SetStatus(Label label, string text, Color? color = null)
    {
        if (label.IsDisposed)
        {
            return;
        }

        label.Text = text;
        label.ForeColor = color ?? Theme.Muted;
    }
}
