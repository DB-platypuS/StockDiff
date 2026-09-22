// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: F6 单元格复制 · Core 剪贴板端口的 WinForms 实现，把系统剪贴板封装为可注入依赖。

using StockDiff.Core.Copy;

namespace StockDiff.App.Services;

// 使用 WinForms Clipboard 写入纯文本；只在 UI 线程（STA）调用
internal sealed class WinClipboard : IClipboard
{
    public void SetText(string text) => Clipboard.SetText(text);
}
