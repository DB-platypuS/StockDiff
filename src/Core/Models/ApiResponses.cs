// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 后端接口响应包装类型，覆盖登录与库存差异两个接口的统一 code/message/data 结构。

using System.Text.Json.Serialization;

namespace StockDiff.Core.Models;

// 登录接口响应：code=0 表示成功，data 内含 token
public sealed class LoginResponse
{
    [JsonPropertyName("code")]    public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    [JsonPropertyName("data")]    public LoginData? Data { get; set; }
}

// 登录成功后的数据体，仅取 token 字段
public sealed class LoginData
{
    [JsonPropertyName("token")] public string Token { get; set; } = "";
}

// 库存差异查询响应：data 为记录列表，可能为空数组
public sealed class StockDiffResponse
{
    [JsonPropertyName("code")]    public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    [JsonPropertyName("data")]    public List<StockDiff> Data { get; set; } = new();
}