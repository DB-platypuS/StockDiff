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

    private static ApiClient NewClient(StubHandler handler, string baseUrl = BaseUrl) => new(baseUrl, handler);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
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
    public async Task LoginAsync_BlankInput_ThrowsArgumentExceptionWithoutRequest(string username, string password)
    {
        var called = false;
        var handler = new StubHandler(_ => { called = true; return Json(HttpStatusCode.OK, "{}"); });
        var client = NewClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.LoginAsync(username, password));
        Assert.False(called);
    }

    [Fact]
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
    public async Task LoginAsync_NullData_ThrowsApiException()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":0,"message":"success","data":null}"""));
        var client = NewClient(handler);

        await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));
    }

    [Fact]
    public async Task LoginAsync_HttpError_ThrowsApiException()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "boom"));
        var client = NewClient(handler);

        await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));
    }

    [Fact]
    public void ClearTokenIf_OnlyClearsMatchingToken()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "{}")));
        client.SetToken("A");

        client.ClearTokenIf("B");
        Assert.Equal("A", client.Token);

        client.ClearTokenIf("A");
        Assert.Equal("", client.Token);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

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