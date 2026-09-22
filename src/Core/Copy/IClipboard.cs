// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: F6 单元格复制 · 剪贴板写入抽象，Core 层零 UI 依赖，具体实现由 App 层注入。

namespace StockDiff.Core.Copy;

/// <summary>剪贴板写入端口：把文本写入系统剪贴板；写入失败由实现抛出异常，交由服务统一收口。</summary>
public interface IClipboard
{
    /// <summary>写入纯文本；失败（如剪贴板被其它进程占用）可抛出异常。</summary>
    void SetText(string text);
}
