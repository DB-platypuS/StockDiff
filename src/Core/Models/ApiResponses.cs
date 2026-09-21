// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 后端接口响应包装类型，覆盖登录与库存差异两个接口的统一 code/message/data 结构。

using System.Text.Json.Serialization;

namespace StockDiff.Core.Models;

public sealed class LoginResponse
{
    [JsonPropertyName("code")]    public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    [JsonPropertyName("data")]    public LoginData? Data { get; set; }
}

public sealed class LoginData
{
    [JsonPropertyName("token")] public string Token { get; set; } = "";
}

public sealed class StockDiffResponse
{
    [JsonPropertyName("code")]    public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    [JsonPropertyName("data")]    public List<StockDiff> Data { get; set; } = new();
}