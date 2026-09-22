// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 展示层纯转换函数，负责仓库代码与中文标签互转，以及 JsonElement 数量转显示字符串。

using System.Text.Json;

namespace StockDiff.Core.Convert;

public static class Converters
{
    // 仓库中文标签口径唯一定义：UI 与 CSV 导出统一引用常量，避免同一字面量在多处重复
    public const string LabelAll = "全部";
    public const string LabelFc = "方仓";
    public const string LabelAsrs = "立库";

    public static string WarehouseLabelFromCode(string? code) => (code ?? "").ToLowerInvariant() switch
    {
        "fc"   => LabelFc,
        "asrs" => LabelAsrs,
        "all"  => LabelAll,
        _      => LabelAll
    };

    public static string WarehouseCodeFromLabel(string? label) => (label ?? "") switch
    {
        LabelFc   => "fc",
        LabelAsrs => "asrs",
        LabelAll  => "all",
        _         => "all"
    };

    public static string NumberToString(JsonElement n) => n.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => "",
        JsonValueKind.Number => n.GetRawText(),
        JsonValueKind.String => n.GetString() ?? "",
        _                    => n.ToString()
    };
}