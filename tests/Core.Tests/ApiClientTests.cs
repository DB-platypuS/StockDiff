// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: ApiClient 登录认证单元测试，覆盖成功取令牌、请求构造、业务错误码、缺令牌、空入参与 HTTP 非 200。

using System.Net;
using System.Text;
using System.Text.Json;
using StockDiff.Core.Api;
using Xunit;

namespace Core.Tests;

public sealed class ApiClientTests
{
    private const string BaseUrl = "http://127.0.0.1:7880";

    // 测试辅助：用 stub handler 构造待测客户端
    private static ApiClient NewClient(StubHandler handler, string baseUrl = BaseUrl) => new(baseUrl, handler);

    // 测试辅助：构造带指定状态码与 JSON 正文的响应
    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    // 正常流程：登录成功返回 token，并写入客户端内部令牌
    public async Task LoginAsync_Success_ReturnsAndStoresToken()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":0,"message":"success","data":{"token":"T-123"}}"""));
        var client = NewClient(handler);

        var token = await client.LoginAsync("000", "0000");

        Assert.Equal("T-123", token);
        Assert.Equal("T-123", client.Token);
    }

    [Fact]
    // 正常流程：请求为 POST；URL 拼接正确（尾斜杠不产生双斜杠）；body 键名小写且账号已 Trim
    public async Task LoginAsync_PostsToLoginUrlWithTrimmedCredentials()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":0,"message":"success","data":{"token":"T"}}"""));
        var client = NewClient(handler, BaseUrl + "/");

        await client.LoginAsync("  000  ", "0000");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(BaseUrl + "/api/v1/auth/login", handler.LastRequest!.RequestUri!.ToString());
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("000", body.RootElement.GetProperty("username").GetString());
        Assert.Equal("0000", body.RootElement.GetProperty("password").GetString());
    }

    [Theory]
    [InlineData("", "0000")]
    [InlineData("   ", "0000")]
    [InlineData("000", "")]
    [InlineData("000", "   ")]
    // 边界：空或纯空白账号/密码抛 ArgumentException，且不发出任何请求
    public async Task LoginAsync_BlankInput_ThrowsArgumentExceptionWithoutRequest(string username, string password)
    {
        var called = false;
        var handler = new StubHandler(_ => { called = true; return Json(HttpStatusCode.OK, "{}"); });
        var client = NewClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.LoginAsync(username, password));
        Assert.False(called);
    }

    [Fact]
    // 异常：业务码非 0 时透出服务端 message，且不写入令牌
    public async Task LoginAsync_NonZeroCode_ThrowsWithServerMessage()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":1001,"message":"账号或密码错误","data":null}"""));
        var client = NewClient(handler);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "bad"));

        Assert.Equal("账号或密码错误", ex.Message);
        Assert.Equal("", client.Token);
    }

    [Fact]
    // 异常：code=0 但 data 为 null（无 token）抛 ApiException
    public async Task LoginAsync_NullData_ThrowsApiException()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":0,"message":"success","data":null}"""));
        var client = NewClient(handler);

        await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));
    }

    [Fact]
    // 异常：HTTP 非 200 抛 ApiException
    public async Task LoginAsync_HttpError_ThrowsApiException()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "boom"));
        var client = NewClient(handler);

        await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    // 异常：空白或非法接口地址抛 ApiException，且文案含 URL 排查提示
    public async Task LoginAsync_InvalidBaseUrl_ThrowsApiExceptionWithUrlHint(string baseUrl)
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{}"));
        var client = NewClient(handler, baseUrl);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Contains("URL错误", ex.Message);
    }

    [Fact]
    // 边界：仅当传入令牌与当前令牌一致时才清空
    public void ClearTokenIf_OnlyClearsMatchingToken()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "{}")));
        client.SetToken("A");

        client.ClearTokenIf("B");
        Assert.Equal("A", client.Token);

        client.ClearTokenIf("A");
        Assert.Equal("", client.Token);
    }

    [Fact]
    public void ClearToken_ResetsToEmpty()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "{}")));
        client.SetToken("A");

        client.ClearToken();

        Assert.Equal("", client.Token);
    }

    [Fact]
    public async Task LoginAsync_Timeout_WrapsAsApiExceptionWithTimeoutHint()
    {
        var client = NewClient(new StubHandler(_ => throw new TaskCanceledException("boom")));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Contains("请求超时", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_HttpRequestException_WrapsAsApiException()
    {
        var client = NewClient(new StubHandler(_ => throw new HttpRequestException("发送请求时出错")));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Contains("发送请求时出错", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_UserCancel_PropagatesOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        var handler = new StubHandler(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });
        var client = NewClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.LoginAsync("000", "0000", cts.Token));
    }

    [Fact]
    public async Task LoginAsync_InvalidJson_ThrowsApiException()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "not-json")));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Contains("响应解析失败", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_NullJsonBody_ThrowsApiException()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "null")));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Contains("响应为空", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoginAsync_NonZeroCodeWithoutMessage_FallsBackToErrorCode(string message)
    {
        var body = "{\"code\":1001,\"message\":\"" + message + "\",\"data\":null}";
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, body)));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Equal("登录失败: 错误码 1001", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_BlankToken_ThrowsApiException()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":0,"message":"success","data":{"token":"   "}}""")));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Contains("未返回token", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_MalformedBaseUrl_WrapsAsApiExceptionWithUrlHint()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "{}")), "http://[::1");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Contains("URL错误", ex.Message);
    }

    // 测试替身：拦截请求并保留最近一次的请求对象与请求体，供断言使用
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        // responder 决定本次请求返回何种响应
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        // 记录请求与请求体后再交由 responder 生成响应
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return _responder(request);
        }
    }
}