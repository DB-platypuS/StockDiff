// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 后端接口响应包装类型，覆盖登录与库存差异两个接口的统一 code/message/data 结构。

using System.Text.Json.Serialization;

namespace StockDiff.Core.Models;

// 登录接口响应：code=0 表示成功，data 内含 token
public sealed class LoginResponse
{
    private string _message = "";

    [JsonPropertyName("code")] public int Code { get; set; }

    // JSON 显式返回 null 时归零为空串，避免调用方按非空字符串处理时抛 NullReferenceException
    [JsonPropertyName("message")]
    public string Message { get => _message; set => _message = value ?? ""; }

    [JsonPropertyName("data")] public LoginData? Data { get; set; }
}

// 登录成功后的数据体，仅取 token 字段
public sealed class LoginData
{
    [JsonPropertyName("token")] public string Token { get; set; } = "";
}

// 库存差异查询响应：data 为记录列表；后端返回 null 时归零为空列表，保证调用方无需判空
public sealed class StockDiffResponse
{
    private List<StockDiffRow> _data = new();

    private string _message = "";

    [JsonPropertyName("code")] public int Code { get; set; }

    // JSON 显式返回 null 时归零为空串，避免 ApiClient 判定「message 是否含 token」时抛 NullReferenceException
    [JsonPropertyName("message")]
    public string Message { get => _message; set => _message = value ?? ""; }

    [JsonPropertyName("data")]
    public List<StockDiffRow> Data
    {
        get => _data;
        set => _data = value ?? new();
    }
}