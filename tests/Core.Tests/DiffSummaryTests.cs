// 创建者: PlatyPus
// 创建时间: 2026-09-29
// 作用: DiffSummary 单元测试，锁定按严重度分组计数的口径（含 Normal 行只计入 Total、null / 空集合 → 全零）。

using System.Text.Json;
using StockDiff.Core.Models;
using StockDiff.Core.Table;
using Xunit;

namespace Core.Tests;

public sealed class DiffSummaryTests
{
    [Fact]
    // null 输入 → 全零摘要
    public void From_Null_IsEmpty()
    {
        Assert.Equal(DiffSummary.Empty, DiffSummary.From(null));
    }

    [Fact]
    // 空集合 → 全零摘要
    public void From_Empty_IsEmpty()
    {
        Assert.Equal(DiffSummary.Empty, DiffSummary.From(Array.Empty<StockDiffRow>()));
    }

    [Fact]
    // 按严重度分组：数量差异=严重、储位差异=警告、效期差异=提示；无差异行只计入 Total
    public void From_MixedRows_CountsBySeverity()
    {
        var rows = new[]
        {
            new StockDiffRow { QtyDiff = Number("-2") },
            new StockDiffRow { Location = "A-01", WarehouseNo = "B-02" },
            new StockDiffRow { DiffType = "效期不一致" },
            new StockDiffRow()
        };

        var summary = DiffSummary.From(rows);

        Assert.Equal(4, summary.Total);
        Assert.Equal(1, summary.Critical);
        Assert.Equal(1, summary.Warning);
        Assert.Equal(1, summary.Notice);
    }

    // 构造数值 JsonElement 的测试辅助
    private static JsonElement Number(string raw) => JsonDocument.Parse(raw).RootElement;
}
