// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: CsvExporter 边界 / 极端值 / 并发 / 失败传播的补充单元测试，补齐 CsvExporterTests
//       未覆盖的取值域：BOM 唯一性、CRLF 终止、CR-only 转义、逗号与引号同时出现、空白单元格保列、
//       流不被接管、超长字段、大量行、null 行、写出失败传播与并发写入一致性。

using StockDiff.Core.Export;
using StockDiff.Core.Models;
using Xunit;
using static Core.Tests.CsvTestSupport;

namespace Core.Tests;

public sealed class CsvExporterEdgeCaseTests
{
    // 原始导出适配：仅取字节与文本供「原样」断言；导出逻辑统一在 CsvTestSupport
    private static (byte[] Bytes, string Text) Raw(IReadOnlyList<StockDiffRow>? rows)
    {
        var (bytes, text, _) = Export(rows);
        return (bytes, text);
    }

    [Fact]
    // 边界：BOM 只出现在文件头部一次，不在每条记录前重复
    public void Write_BomAppearsExactlyOnce()
    {
        var (_, text) = Raw(new[] { Row(code: "A"), Row(code: "B") });

        Assert.StartsWith("\uFEFF", text);
        Assert.Equal(1, text.Count(c => c == '\uFEFF'));
    }

    [Fact]
    // 边界：每条记录（含表头）均以 CRLF 终止，末尾保留 CRLF（RFC 4180）
    public void Write_EveryRecordTerminatedWithCrlf()
    {
        var (_, text) = Raw(new[] { Row(code: "A"), Row(code: "B") });

        Assert.EndsWith("\r\n", text);
        var records = text.TrimStart('\uFEFF').Split("\r\n");
        Assert.Equal(4, records.Length);   // 表头 + 2 数据行 + 末尾空串
        Assert.Equal("", records[^1]);
    }

    [Fact]
    // 边界：仅含回车（\r）的字段同样触发引号包裹（MustQuote 含 '\r'）
    public void Write_FieldWithCarriageReturnOnly_IsQuoted()
    {
        var (_, text) = Raw(new[] { Row(code: "A\rB") });

        Assert.Contains("\"A\rB\"", text);
    }

    [Fact]
    // 转义：字段同时含逗号与双引号 → 包裹一次 + 内部引号翻倍
    public void Write_FieldWithCommaAndQuote_EscapesBoth()
    {
        var (_, text) = Raw(new[] { Row(code: "A,\"B") });

        Assert.Contains("\"A,\"\"B\"", text);
    }

    [Fact]
    // 边界：单个空格属于「非空」→ 该列保留，值原样输出（与空串被过滤形成对照）
    public void Write_WhitespaceOnlyCell_KeepsColumn()
    {
        var (_, text) = Raw(new[] { Row(code: "A", location: " ") });

        Assert.StartsWith("物料编码,仓库类型,储位", text.TrimStart('\uFEFF'));
    }

    [Fact]
    // 边界：过滤空列不影响保留列的转义结果
    public void Write_FilteredEmptyColumn_DoesNotAffectEscapingOfKeptColumn()
    {
        var (_, text) = Raw(new[] { Row(code: "A,B") });

        Assert.Equal("物料编码,仓库类型", text.TrimStart('\uFEFF').Split("\r\n")[0]);
        Assert.Contains("\"A,B\"", text);
    }

    [Fact]
    // 边界：导出后不接管调用方流的生命周期（leaveOpen），流仍可读写
    public void Write_LeavesOutputStreamOpen()
    {
        using var stream = new MemoryStream();

        CsvExporter.Write(stream, new[] { Row(code: "A") });

        Assert.True(stream.CanWrite);
        Assert.True(stream.CanRead);
    }

    [Fact]
    // 极端值：单个超长字段（10 万字符）完整保留，不截断
    public void Write_OverlongField_PreservesWholeValue()
    {
        var value = new string('A', 100_000);

        var (_, text) = Raw(new[] { Row(code: value) });

        Assert.Contains(value, text);
    }

    [Fact]
    // 极端值：5000 行 → 记录数 = 表头 + 全部数据行，且顺序保持
    public void Write_ManyRows_ProducesHeaderPlusAllRecords()
    {
        var rows = Enumerable.Range(0, 5000).Select(i => Row(code: $"M{i}")).ToArray();

        var (_, text) = Raw(rows);
        var records = text.TrimStart('\uFEFF').TrimEnd('\r', '\n').Split("\r\n");

        Assert.Equal(5001, records.Length);
        Assert.Equal("M4999", records[5000].Split(',')[0]);
    }

    [Fact]
    // 边界：数据中夹带 null 行 → 渲染为一条全空记录，不抛异常
    public void Write_NullRowAmongData_ProducesEmptyRecord()
    {
        var rows = new StockDiffRow[] { Row(code: "A"), null! };

        var (_, text) = Raw(rows);
        var records = text.TrimStart('\uFEFF').TrimEnd('\r', '\n').Split("\r\n");

        Assert.Equal("物料编码,仓库类型", records[0]);
        Assert.Equal("A,全部", records[1]);
        Assert.Equal(",", records[2]);
    }

    [Fact]
    // 编码：非 ASCII（中文 + emoji）经 UTF-8 往返无损
    public void Write_NonAsciiContent_RoundTripsViaUtf8()
    {
        var (bytes, text) = Raw(new[] { Row(code: "中文😀") });

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        Assert.Contains("中文😀", text);
    }

    [Fact]
    // 异常：入参校验顺序固定 —— output 为 null 时优先抛 ArgumentNullException（即便 rows 也为 null）
    public void Write_NullOutputWithNullRows_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CsvExporter.Write(null!, null));
    }

    [Fact]
    // 异常：底层流写出失败 → IOException 向外传播，Core 层不吞异常（由 App 层统一收口）
    public void Write_StreamThrowsOnWrite_Propagates()
    {
        // 字段远大于 1024 缓冲，确保写入在 WriteRecord 内即触达底层流而非延迟到 Dispose
        var rows = new[] { Row(code: new string('X', 3000)) };

        Assert.Throws<IOException>(() => CsvExporter.Write(new ThrowingStream(), rows));
    }

    [Fact]
    // 并发：静态方法无可变共享状态，多线程分别写入各自流，输出完全一致
    public void Write_ConcurrentWrites_ProduceIdenticalOutput()
    {
        var rows = Enumerable.Range(0, 50).Select(i => Row(code: $"M{i}", location: $"L{i}")).ToArray();
        var (expected, _) = Raw(rows);
        var results = new byte[16][];

        Parallel.For(0, results.Length, i =>
        {
            using var stream = new MemoryStream();
            CsvExporter.Write(stream, rows);
            results[i] = stream.ToArray();
        });

        foreach (var actual in results)
        {
            Assert.Equal(expected, actual);
        }
    }

    // 测试替身：任何写入都抛 IOException，模拟磁盘/管道写出失败
    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new IOException("磁盘写入失败");
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("磁盘写入失败");
    }
}