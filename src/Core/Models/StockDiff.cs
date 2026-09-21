// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 单条库存差异记录实体，字段与后端 /api/v1/stock/diff 响应一一对应；
//       三个数量字段使用 JsonElement 保留大数精度，禁止使用 double。

using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockDiff.Core.Models;

public sealed class StockDiff
{
    [JsonPropertyName("material_code")]    public string MaterialCode    { get; set; } = "";
    [JsonPropertyName("diff_type")]        public string DiffType        { get; set; } = "";
    [JsonPropertyName("warehouse_type")]   public string WarehouseType   { get; set; } = "";
    [JsonPropertyName("warehouse_qty")]    public JsonElement WarehouseQty { get; set; }
    [JsonPropertyName("wms_qty")]          public JsonElement WmsQty       { get; set; }
    [JsonPropertyName("qty_diff")]         public JsonElement QtyDiff      { get; set; }
    [JsonPropertyName("location")]         public string Location        { get; set; } = "";
    [JsonPropertyName("warehouse_no")]     public string WarehouseNo     { get; set; } = "";
    [JsonPropertyName("warehouse_hold")]   public string WarehouseHold   { get; set; } = "";
    [JsonPropertyName("wms_hold")]         public string WmsHold         { get; set; } = "";
    [JsonPropertyName("warehouse_expiry")] public string WarehouseExpiry { get; set; } = "";
    [JsonPropertyName("wms_expiry")]       public string WmsExpiry       { get; set; } = "";
}