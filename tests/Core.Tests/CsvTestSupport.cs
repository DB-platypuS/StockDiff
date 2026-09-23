// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: CSV 导出相关测试的公共辅助：构造 JsonElement 数字、StockDiffRow 样例与「导出到内存流」的统一入口，
//       供 CsvExporterTests / CsvExporterEdgeCaseTests 复用，避免同一辅助在多个测试类各写一遍。

using System.Text;
using System.Text.Json;
using StockDiff.Core.Export;
using StockDiff.Core.Models;

namespace Core.Tests;

internal static class CsvTestSupport
{
    // 解析 JSON 数字并克隆为独立 JsonElement，保留原始文本（不做 double 转换，避免精度丢失）
    public static JsonElement Num(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
    }

    // 构造测试记录：仅设置被测字段，未指定字段保持默认空值
    public static StockDiffRow Row(string code = "", string diffType = "", string location = "") =>
        new() { MaterialCode = code, DiffType = diffType, Location = location };

    // 导出到内存流并拆分：Bytes 留原始字节（验 BOM），Records 去掉 BOM 与尾部换行后按 CRLF 分行
    public static (byte[] Bytes, string Text, string[] Records) Export(IReadOnlyList<StockDiffRow>? rows)
    {
        using var stream = new MemoryStream();
        CsvExporter.Write(stream, rows);
        var bytes = stream.ToArray();
        var text = Encoding.UTF8.GetString(bytes);
        var records = text.TrimStart('\uFEFF').TrimEnd('\r', '\n').Split("\r\n");
        return (bytes, text, records);
    }
}
