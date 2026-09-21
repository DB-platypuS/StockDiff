// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 展示层纯转换函数，负责仓库代码与中文标签互转，以及 JsonElement 数量转显示字符串。

using System.Text.Json;

namespace StockDiff.Core.Convert;

public static class Converters
{
    public static string WarehouseLabelFromCode(string? code) => (code ?? "").ToLowerInvariant() switch
    {
        "fc"   => "方仓",
        "asrs" => "立库",
        "all"  => "全部",
        _      => "全部"
    };

    public static string WarehouseCodeFromLabel(string? label) => (label ?? "") switch
    {
        "方仓" => "fc",
        "立库" => "asrs",
        "全部" => "all",
        _      => "all"
    };

    public static string NumberToString(JsonElement n) => n.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => "",
        JsonValueKind.Number => n.GetRawText(),
        JsonValueKind.String => n.GetString() ?? "",
        _                    => n.ToString()
    };
}