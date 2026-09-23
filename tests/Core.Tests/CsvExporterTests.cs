// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: CsvExporter 单元测试，锁定导出契约：UTF-8 BOM、按 TableColumns 派生表头与数据、
//       全空列过滤与全空保列、RFC 4180 转义，以及空输入与非法入参的处理。

using StockDiff.Core.Export;
using StockDiff.Core.Models;
using StockDiff.Core.Table;
using Xunit;
using static Core.Tests.CsvTestSupport;

namespace Core.Tests;

public sealed class CsvExporterTests
{
    [Fact]
    // 正常：导出内容以 UTF-8 BOM 开头
    public void Write_WithRows_StartsWithUtf8Bom()
    {
        var (bytes, _, _) = Export(new[] { Row(code: "AC001") });

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
    }

    [Fact]
    // 正常：表头与数据行由 TableColumns 派生，仓库类型经转换器输出中文
    public void Write_WritesHeaderAndDataRecords()
    {
        var (_, _, records) = Export(new[] { Row(code: "AC001", diffType: "差异") });

        Assert.Equal("物料编码,异常种类,仓库类型", records[0]);
        Assert.Equal("AC001,差异,全部", records[1]);
        Assert.Equal(2, records.Length);
    }

    [Fact]
    // 边界：整列无值被过滤，部分有值的列保留，缺值单元格留空
    public void Write_EmptyColumnFiltered_NonEmptyColumnKept()
    {
        var (_, _, records) = Export(new[] { Row(code: "A"), Row(location: "L1") });

        Assert.Equal("物料编码,仓库类型,储位", records[0]);
        Assert.Equal("A,全部,", records[1]);
        Assert.Equal(",全部,L1", records[2]);
    }

    [Fact]
    // 边界：全部行所有单元格为空（null 行）→ 保留全部 12 列
    public void Write_AllCellsEmpty_KeepsAllColumns()
    {
        var (_, _, records) = Export(new StockDiffRow[] { null! });

        Assert.Equal(string.Join(',', TableColumns.Headers()), records[0]);
        Assert.Equal(new string(',', 11), records[1]);
    }

    [Fact]
    // 边界：null 输入不写任何内容（无 BOM）
    public void Write_NullRows_WritesNothing()
    {
        var (bytes, _, _) = Export(null);

        Assert.Empty(bytes);
    }

    [Fact]
    // 边界：空集合不写任何内容
    public void Write_EmptyRows_WritesNothing()
    {
        var (bytes, _, _) = Export(Array.Empty<StockDiffRow>());

        Assert.Empty(bytes);
    }

    [Fact]
    // 转义：含逗号的字段用双引号包裹
    public void Write_FieldWithComma_IsQuoted()
    {
        var (_, _, records) = Export(new[] { Row(code: "A,B") });

        Assert.Contains("\"A,B\"", records[1]);
    }

    [Fact]
    // 转义：含双引号的字段包裹并将内部引号翻倍
    public void Write_FieldWithQuote_DoublesQuotes()
    {
        var (_, _, records) = Export(new[] { Row(code: "A\"B") });

        Assert.Contains("\"A\"\"B\"", records[1]);
    }

    [Fact]
    // 转义：含换行的字段用双引号包裹（按原始文本断言，避免被分行干扰）
    public void Write_FieldWithNewline_IsQuoted()
    {
        var (_, text, _) = Export(new[] { Row(code: "A\r\nB") });

        Assert.Contains("\"A\r\nB\"", text);
    }

    [Fact]
    // 正常：JsonElement 数字保留原始文本，不做精度损失转换
    public void Write_NumberField_PreservesRawText()
    {
        var (_, _, records) = Export(new[] { new StockDiffRow { QtyDiff = Num("12345678901234567890") } });

        Assert.Contains("12345678901234567890", records[1]);
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+A1")]
    [InlineData("@cmd")]
    [InlineData("-1+1")]
    // 安全：以公式起始字符开头且非纯数字的字段前置单引号，避免 Excel 执行（CWE-1236）
    public void Write_FormulaLikeField_IsNeutralized(string value)
    {
        var (_, text, _) = Export(new[] { Row(code: value) });

        Assert.Contains("'" + value, text);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("+3.5")]
    // 安全：正负号开头的纯数字属数值而非公式，保持原样不中和（数量字段语义不变）
    public void Write_SignedNumericField_IsNotNeutralized(string value)
    {
        var (_, text, _) = Export(new[] { Row(code: value) });

        Assert.DoesNotContain("'" + value, text);
    }

    [Fact]
    // 异常：null 流 → ArgumentNullException
    public void Write_NullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CsvExporter.Write(null!, new[] { Row(code: "A") }));
    }

    [Fact]
    // 异常：不可写流 → ArgumentException
    public void Write_NonWritableStream_Throws()
    {
        using var stream = new MemoryStream(Array.Empty<byte>(), writable: false);

        Assert.Throws<ArgumentException>(() => CsvExporter.Write(stream, new[] { Row(code: "A") }));
    }

    [Fact]
    // 正常：默认文件名含时间戳与仓库标签后缀
    public void BuildDefaultFileName_FormatsName()
    {
        var name = CsvExporter.BuildDefaultFileName("方仓");

        Assert.Matches(@"^\d{8}_\d{6}_", name);
        Assert.EndsWith("_方仓&WMS差异情况.csv", name);
    }

    [Fact]
    // 边界：标签为空 / null → 回退「全部」
    public void BuildDefaultFileName_EmptyLabel_FallsBackToAll()
    {
        Assert.EndsWith("_全部&WMS差异情况.csv", CsvExporter.BuildDefaultFileName(""));
        Assert.EndsWith("_全部&WMS差异情况.csv", CsvExporter.BuildDefaultFileName(null));
    }
}
