// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 库存差异系统 HTTP 客户端，封装接口地址与令牌状态，实现登录认证并统一网络与协议错误处理。

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StockDiff.Core.Config;
using StockDiff.Core.Models;

namespace StockDiff.Core.Api;

public sealed class ApiClient
{
    private static readonly HttpClient SharedHttp = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly object _lock = new();
    private readonly HttpClient _http;
    private string _baseUrl;
    private string _token = "";

    public ApiClient(string baseUrl)
        : this(baseUrl, SharedHttp)
    {
    }

    internal ApiClient(string baseUrl, HttpMessageHandler handler)
        : this(baseUrl, new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan })
    {
    }

    private ApiClient(string baseUrl, HttpClient http)
    {
        _baseUrl = baseUrl;
        _http = http;
    }

    public string BaseUrl
    {
        get { lock (_lock) { return _baseUrl; } }
        set { lock (_lock) { _baseUrl = value; } }
    }

    public string Token
    {
        get { lock (_lock) { return _token; } }
    }

    public void SetToken(string token)
    {
        lock (_lock) { _token = token ?? ""; }
    }

    public void ClearToken()
    {
        lock (_lock) { _token = ""; }
    }

    public void ClearTokenIf(string oldToken)
    {
        lock (_lock)
        {
            if (_token == oldToken)
            {
                _token = "";
            }
        }
    }

    public async Task<string> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("用户名和密码不能为空");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(AppConfig.LoginTimeout);

        var body = JsonSerializer.Serialize(new LoginRequest(username.Trim(), password));
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(FullUrl("/auth/login"), content, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(NetworkErrorMapper.Map(ex), ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(NetworkErrorMapper.Map(ex), ex);
        }
        catch (UriFormatException ex)
        {
            throw new ApiException(NetworkErrorMapper.Map(ex), ex);
        }

        using (response)
        {
            if ((int)response.StatusCode != 200)
            {
                throw new ApiException($"登录失败: HTTP {(int)response.StatusCode}");
            }

            LoginResponse? parsed;
            try
            {
                var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                parsed = JsonSerializer.Deserialize<LoginResponse>(json);
            }
            catch (JsonException ex)
            {
                throw new ApiException("登录失败: 响应解析失败", ex);
            }

            if (parsed is null)
            {
                throw new ApiException("登录失败: 响应为空");
            }

            if (parsed.Code != 0)
            {
                throw new ApiException(string.IsNullOrWhiteSpace(parsed.Message)
                    ? $"登录失败: 错误码 {parsed.Code}"
                    : parsed.Message);
            }

            var token = parsed.Data?.Token ?? "";
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ApiException("登录失败: 未返回token");
            }

            SetToken(token);
            return token;
        }
    }

    private string FullUrl(string path) => BaseUrl.TrimEnd('/') + AppConfig.ApiPrefix + path;
}

internal sealed record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);