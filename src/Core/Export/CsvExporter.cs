// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: F7 CSV 导出 · 纯逻辑：把库存差异记录按 TableColumns 派生为 RFC 4180 CSV 并写入流，
//       首部写 UTF-8 BOM 保证 Excel 中文不乱码，过滤全空列；零 UI 依赖，可脱离界面单测。

using System.Globalization;
using System.Text;
using StockDiff.Core.Convert;
using StockDiff.Core.Models;
using StockDiff.Core.Table;

namespace StockDiff.Core.Export;

// CSV 导出器：表头与数据行全部由 TableColumns 派生，保证「表格展示」与「导出文件」同源
public static class CsvExporter
{
    // 记录分隔符：RFC 4180 规定 CRLF，Excel 亦以此识别换行
    private const string RecordSeparator = "\r\n";

    // 触发引号包裹的字符：逗号、双引号、CR、LF
    private static readonly char[] MustQuote = { ',', '"', '\r', '\n' };

    // 会被 Excel 当作公式起始的字符，命中即前置单引号中和（CWE-1236）
    private const string ExpressionLead = "=+-@";

    /// <summary>
    /// 把记录写入 CSV 流：写 UTF-8 BOM；表头与数据由 TableColumns 派生；过滤全空列（过滤后无列时保留全部列）。
    /// rows 为 null / 空 → 不写任何内容（含 BOM），由调用方提示「暂无数据」。
    /// </summary>
    public static void Write(Stream output, IReadOnlyList<StockDiffRow>? rows)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (rows is null || rows.Count == 0)
        {
            return;
        }

        var grid = TableGrid.From(rows);
        var keep = NonEmptyColumns(grid.Rows, grid.ColumnCount);

        // leaveOpen: 只借用流写内容，不接管其生命周期（文件关闭由 App 层负责）
        using var writer = new StreamWriter(
            output,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            bufferSize: 1024,
            leaveOpen: true);

        WriteRecord(writer, grid.Headers, keep);
        foreach (var row in grid.Rows)
        {
            WriteRecord(writer, row, keep);
        }
    }

    /// <summary>构造导出默认文件名：yyyyMMdd_HHmmss_仓库标签&amp;WMS差异情况.csv；标签为空回退「全部」。</summary>
    public static string BuildDefaultFileName(string? warehouseLabel)
    {
        var label = string.IsNullOrWhiteSpace(warehouseLabel) ? Converters.LabelAll : warehouseLabel;

        // 固定区域性：自定义格式串会随当前区域性日历变化（如佛历/回历系统年份偏移），
        // 不固定会在非公历系统上生成错误年份，故显式使用 InvariantCulture 得到稳定 ASCII 日期
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return $"{stamp}_{label}&WMS差异情况.csv";
    }

    // 逐列判定是否「全空」：该列在任意一行有非空值即保留；若所有列都空则保留全部列，避免只剩空表头
    private static bool[] NonEmptyColumns(IReadOnlyList<string[]> rows, int columnCount)
    {
        var keep = new bool[columnCount];
        var anyKept = false;

        for (var col = 0; col < columnCount; col++)
        {
            foreach (var row in rows)
            {
                if (!string.IsNullOrEmpty(row[col]))
                {
                    keep[col] = true;
                    anyKept = true;
                    break;
                }
            }
        }

        if (!anyKept)
        {
            Array.Fill(keep, true);
        }

        return keep;
    }

    // 按保留列写出一条记录并追加 CRLF；字段按 RFC 4180 转义
    private static void WriteRecord(TextWriter writer, IReadOnlyList<string> cells, IReadOnlyList<bool> keep)
    {
        var first = true;
        for (var col = 0; col < keep.Count; col++)
        {
            if (!keep[col])
            {
                continue;
            }

            if (!first)
            {
                writer.Write(',');
            }

            writer.Write(Escape(cells[col]));
            first = false;
        }

        writer.Write(RecordSeparator);
    }

    // RFC 4180：含逗号 / 双引号 / 换行时用双引号包裹，内部双引号翻倍；写出前先中和公式注入
    private static string Escape(string field)
    {
        var value = NeutralizeFormula(field);
        return value.IndexOfAny(MustQuote) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    // 中和 CSV 公式注入（CWE-1236）：以 = + - @ 开头的字段前置单引号，使 Excel 按文本处理；
    // 纯数字（含正负号，如数量字段 -1）属数值而非公式，保持原样，不破坏数值语义
    private static string NeutralizeFormula(string field)
    {
        if (field.Length == 0 || ExpressionLead.IndexOf(field[0]) < 0)
        {
            return field;
        }

        return decimal.TryParse(field, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
            ? field
            : "'" + field;
    }
}
