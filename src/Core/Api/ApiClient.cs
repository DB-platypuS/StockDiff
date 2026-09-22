// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: 库存差异系统 HTTP 客户端，封装接口地址与令牌状态，实现登录认证并统一网络与协议错误处理。

using System.Net.Sockets;
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
    private readonly ITcpConnector _connector;
    private string _baseUrl;
    private string _token = "";

    // 生产用构造函数：复用静态共享 HttpClient，避免每次请求新建导致端口耗尽
    public ApiClient(string baseUrl)
        : this(baseUrl, SharedHttp, new TcpConnector())
    {
    }

    // 测试用构造函数：注入自定义 HttpMessageHandler，使单元测试可脱离真实网络
    internal ApiClient(string baseUrl, HttpMessageHandler handler)
        : this(baseUrl, new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan }, new TcpConnector())
    {
    }

    // 测试用构造函数：注入自定义 TCP 连接器，使连接测试的成功/拒连/超时分支可确定性单测
    internal ApiClient(string baseUrl, ITcpConnector connector)
        : this(baseUrl, SharedHttp, connector)
    {
    }

    // 统一入口：不设全局超时，超时由各方法内的 CancellationTokenSource 控制
    private ApiClient(string baseUrl, HttpClient http, ITcpConnector connector)
    {
        _baseUrl = baseUrl;
        _http = http;
        _connector = connector;
    }

    // 接口根地址，读写均加锁，保证 UI 线程与后台线程看到一致值
    public string BaseUrl
    {
        get { lock (_lock) { return _baseUrl; } }
        set { lock (_lock) { _baseUrl = value; } }
    }

    // 当前登录令牌，只读；未登录时为空串
    public string Token
    {
        get { lock (_lock) { return _token; } }
    }

    // 写入登录令牌（null 归一为空串）
    public void SetToken(string token)
    {
        lock (_lock) { _token = token ?? ""; }
    }

    // 清空令牌，退出登录或地址变更时调用
    public void ClearToken()
    {
        lock (_lock) { _token = ""; }
    }

    // 仅当当前令牌与传入值一致时才清空，防止旧请求的 401 抹掉新会话令牌
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

    // 登录：校验入参 → 20 秒超时 → POST /api/v1/auth/login → 校验业务码与 token → 写入并返回 token
    // 失败统一抛 ApiException（网络类经 NetworkErrorMapper 附加排查建议）；空入参或非法地址另抛对应异常
    public async Task<string> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("用户名和密码不能为空");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(AppConfig.LoginTimeout);

        var loginUrl = FullUrl("/auth/login");
        if (!Uri.TryCreate(loginUrl, UriKind.Absolute, out _))
        {
            throw new ApiException(NetworkErrorMapper.Map(new UriFormatException()));
        }

        try
        {
            var body = JsonSerializer.Serialize(new LoginRequest(username.Trim(), password));
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(loginUrl, content, cts.Token).ConfigureAwait(false);

            if ((int)response.StatusCode != 200)
            {
                throw new ApiException($"登录失败: HTTP {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            var parsed = JsonSerializer.Deserialize<LoginResponse>(json)
                ?? throw new ApiException("登录失败: 响应为空");

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
        catch (JsonException ex)
        {
            throw new ApiException("登录失败: 响应解析失败", ex);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(NetworkErrorMapper.Map(ex), ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(NetworkErrorMapper.Map(ex), ex);
        }
    }

    // 连接测试：解析 host/端口 → TCP 可达性探测（5s 超时）→ 成功静默返回
    // 失败统一抛 ApiException（含排查建议）；用户主动取消则透传 OperationCanceledException
    public async Task TestConnectionAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(AppConfig.ConnectTestTimeout);

        try
        {
            var (host, port) = ResolveHostPort(BaseUrl);
            await _connector.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(NetworkErrorMapper.Map(ex), ex);
        }
        catch (UriFormatException ex)
        {
            throw new ApiException(NetworkErrorMapper.Map(ex), ex);
        }
        catch (SocketException ex)
        {
            throw new ApiException(NetworkErrorMapper.Map(ex), ex);
        }
    }

    // 从接口地址解析主机与端口：无端口时按 scheme 兜底 443/80；地址为空或非法抛 UriFormatException
    internal static (string Host, int Port) ResolveHostPort(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new UriFormatException("接口地址无效");
        }

        var port = uri.IsDefaultPort
            ? (uri.Scheme == Uri.UriSchemeHttps ? 443 : 80)
            : uri.Port;

        return (uri.Host, port);
    }

    // 拼接完整 URL：先去掉 BaseUrl 末尾斜杠，避免出现双斜杠
    private string FullUrl(string path) => BaseUrl.TrimEnd('/') + AppConfig.ApiPrefix + path;
}

// TCP 连接抽象：默认走真实 TcpClient；单测注入替身即可脱离真实网络
internal interface ITcpConnector
{
    Task ConnectAsync(string host, int port, CancellationToken ct);
}

// 默认实现：按主机与端口建立 TCP 连接，超时由调用方传入的取消令牌控制
internal sealed class TcpConnector : ITcpConnector
{
    public async Task ConnectAsync(string host, int port, CancellationToken ct)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, ct).ConfigureAwait(false);
    }
}

// 登录请求体：JSON 字段固定为小写 username / password，与后端契约一致
internal sealed record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);