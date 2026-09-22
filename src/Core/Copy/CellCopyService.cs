// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: F6 单元格复制 · 复制用例逻辑：入参校验、剪贴板写入与统一结果返回，可脱离界面单测。

namespace StockDiff.Core.Copy;

// 复制结果状态：已复制 / 空值未复制 / 写入失败
public enum CopyStatus { Copied, Empty, Failed }

// 单次复制结果：状态、待复制文本与面向用户的统一提示文案
public readonly record struct CopyResult(CopyStatus Status, string Text, string Message);

// 单元格复制服务：把「校验 → 写入 → 结果」收口为纯逻辑，UI 只负责展示 Message
public sealed class CellCopyService
{
    private readonly IClipboard _clipboard;

    public CellCopyService(IClipboard clipboard)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
    }

    /// <summary>
    /// 复制单元格文本：null/空串 → 不写剪贴板并返回 Empty；
    /// 写入成功 → Copied 并携带「已复制: 值」；写入抛错 → Failed 并携带失败文案（不向外抛）。
    /// </summary>
    public CopyResult Copy(string? cellValue)
    {
        if (string.IsNullOrEmpty(cellValue))
        {
            return new CopyResult(CopyStatus.Empty, "", "");
        }

        try
        {
            _clipboard.SetText(cellValue);
        }
        catch (Exception ex)
        {
            return new CopyResult(CopyStatus.Failed, cellValue, $"复制失败: {ex.Message}");
        }

        return new CopyResult(CopyStatus.Copied, cellValue, $"已复制: {cellValue}");
    }
}