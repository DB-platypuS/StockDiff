// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 后端响应契约与 F0 数据管道集成测试：StockDiffResponse 反序列化 → TableColumns 输出 12 列展示值。

using System.Text.Json;
using StockDiff.Core.Models;
using StockDiff.Core.Table;
using Xunit;

namespace Core.Tests;

public sealed class ModelsContractTests
{
    private const string Payload = """
    {"code":0,"message":"success","data":[
      {"material_code":"AC04672026013030856","diff_type":"数量不一致","warehouse_type":"fc",
       "warehouse_qty":2640,"wms_qty":2641,"qty_diff":-1,"location":"3-21-1-2",
       "warehouse_no":"B-01","warehouse_hold":"N","wms_hold":"Y",
       "warehouse_expiry":"20271124","wms_expiry":"20271124","wms_location":"X-9"}
    ]}
    """;

    [Fact]
    public void FullEnvelope_FlowsThroughPipeline_ToTwelveDisplayValues()
    {
        var resp = JsonSerializer.Deserialize<StockDiffResponse>(Payload)!;
        var row = Assert.Single(resp.Data);

        var values = Enumerable.Range(0, 12).Select(i => TableColumns.CellValue(row, i)).ToArray();

        Assert.Equal(new[]
        {
            "AC04672026013030856", "数量不一致", "方仓", "2640", "2641", "-1",
            "3-21-1-2", "B-01", "N", "Y", "20271124", "20271124"
        }, values);
    }

    [Fact]
    public void BigIntegerQty_PreservesPrecisionEndToEnd()
    {
        const string raw = "123456789012345678901234567890";
        var resp = JsonSerializer.Deserialize<StockDiffResponse>(
            """{"code":0,"data":[{"qty_diff":123456789012345678901234567890}]}""")!;

        Assert.Equal(raw, TableColumns.CellValue(resp.Data[0], 5));
    }

    [Fact]
    public void MissingFields_FallBackToDefaults()
    {
        var resp = JsonSerializer.Deserialize<StockDiffResponse>("""{"code":0,"data":[{}]}""")!;
        var row = resp.Data[0];

        Assert.Equal("", row.MaterialCode);
        Assert.Equal("", row.DiffType);
        Assert.Equal("", TableColumns.CellValue(row, 3));
        Assert.Equal("全部", TableColumns.CellValue(row, 2));
    }

    [Fact]
    public void NullData_DeserializesToNull_NotInitializedList()
    {
        var resp = JsonSerializer.Deserialize<StockDiffResponse>("""{"code":0,"message":"ok","data":null}""")!;

        Assert.Null(resp.Data);
    }
}
