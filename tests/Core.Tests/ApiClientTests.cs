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

    [Fact]
    // 边界：BaseUrl 读写原样往返（含末尾斜杠）
    public void BaseUrl_RoundTripsThroughSetter()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "{}")), "http://10.0.0.1:8080/");

        Assert.Equal("http://10.0.0.1:8080/", client.BaseUrl);

        client.BaseUrl = "http://10.0.0.2:9090";

        Assert.Equal("http://10.0.0.2:9090", client.BaseUrl);
    }

    [Fact]
    // 边界：令牌初始为空串
    public void Token_InitiallyEmpty() =>
        Assert.Equal("", NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "{}"))).Token);

    [Fact]
    // 边界：SetToken(null) 归一为空串，Token 永不返回 null
    public void SetToken_Null_BecomesEmpty()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "{}")));

        client.SetToken(null!);

        Assert.Equal("", client.Token);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    // 异常：登录接口返回 401/403 时抛 ApiException（登录阶段不涉及会话失效处理）
    public async Task LoginAsync_UnauthorizedStatus_ThrowsApiException(HttpStatusCode status, int code)
    {
        var client = NewClient(new StubHandler(_ => Json(status, "{}")));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Contains($"HTTP {code}", ex.Message);
    }

    [Fact]
    // 正常流程：登录请求不携带 Authorization 头（此时尚未拿到令牌）
    public async Task LoginAsync_OmitsAuthorizationHeader()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":0,"message":"success","data":{"token":"T"}}"""));
        var client = NewClient(handler);

        await client.LoginAsync("000", "0000");

        Assert.Null(handler.LastRequest!.Headers.Authorization);
    }

    [Fact]
    // 并发：多线程同时读写令牌不应抛异常，最终值只可能是空串或写入值
    public void Token_ConcurrentAccess_StaysConsistent()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "{}")));

        Parallel.For(0, 500, _ =>
        {
            client.SetToken("A");
            client.ClearTokenIf("A");
            Assert.NotNull(client.Token);
        });

        Assert.Contains(client.Token, new[] { "", "A" });
    }

    // ---- F4 数据查询与刷新 ----

    // 单条差异记录样例，覆盖字段解析与数量原文
    private const string DiffJson = """
        {"code":0,"message":"success","data":[
          {"material_code":"M-1","diff_type":"储位不一致","warehouse_type":"fc","warehouse_qty":2640,
           "wms_qty":2640,"qty_diff":0,"location":"3-21-1-2","warehouse_no":"100B","warehouse_hold":"N",
           "wms_hold":"N","warehouse_expiry":"20271124","wms_expiry":"20271124"}
        ]}
        """;

    [Fact]
    // 正常流程：带令牌拉取成功，返回记录条数与关键字段解析正确
    public async Task FetchStockDiffAsync_Success_ReturnsRows()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, DiffJson));
        var client = NewClient(handler);
        client.SetToken("T-123");

        var rows = await client.FetchStockDiffAsync("all", false, false);

        Assert.Single(rows);
        Assert.Equal("M-1", rows[0].MaterialCode);
        Assert.Equal("储位不一致", rows[0].DiffType);
        Assert.Equal("2640", rows[0].WarehouseQty.GetRawText());
        Assert.Equal("100B", rows[0].WarehouseNo);
    }

    [Fact]
    // 正常流程：GET 拼接 /stock/diff 与三个查询参数，并携带 Bearer 令牌
    public async Task FetchStockDiffAsync_BuildsGetRequestWithQueryAndAuth()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, DiffJson));
        var client = NewClient(handler, BaseUrl + "/");
        client.SetToken("T-123");

        await client.FetchStockDiffAsync("asrs", true, true);

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("T-123", handler.LastRequest!.Headers.Authorization!.Parameter);
        Assert.Equal("/api/v1/stock/diff", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("?warehouse=asrs&compare_hold=true&compare_expiry=true", handler.LastRequest!.RequestUri!.Query);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    // 边界：warehouse 空或纯空白兜底为 all
    public async Task FetchStockDiffAsync_BlankWarehouse_FallsBackToAll(string warehouse)
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, DiffJson));
        var client = NewClient(handler);
        client.SetToken("T");

        await client.FetchStockDiffAsync(warehouse, false, false);

        Assert.Equal("?warehouse=all&compare_hold=false&compare_expiry=false", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    // 边界：data 为 null 归一为空列表，调用方无需判空
    public async Task FetchStockDiffAsync_NullData_ReturnsEmptyList()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"code":0,"message":"success","data":null}"""));
        var client = NewClient(handler);
        client.SetToken("T");

        var rows = await client.FetchStockDiffAsync("all", false, false);

        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    [Fact]
    // 边界：超大整数以原始文本保精度，不发生 double 变形
    public async Task FetchStockDiffAsync_BigInteger_PreservesRawText()
    {
        const string big = "12345678901234567890123";
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            "{\"code\":0,\"message\":\"success\",\"data\":[{\"warehouse_qty\":" + big + "}]}"));
        var client = NewClient(handler);
        client.SetToken("T");

        var rows = await client.FetchStockDiffAsync("all", false, false);

        Assert.Equal(big, rows[0].WarehouseQty.GetRawText());
    }

    [Theory]
    [InlineData(false, false, "?warehouse=all&compare_hold=false&compare_expiry=false")]
    [InlineData(true, false, "?warehouse=all&compare_hold=true&compare_expiry=false")]
    [InlineData(false, true, "?warehouse=all&compare_hold=false&compare_expiry=true")]
    [InlineData(true, true, "?warehouse=all&compare_hold=true&compare_expiry=true")]
    // 边界：两个对比开关的全部组合均以小写布尔拼入查询串
    public async Task FetchStockDiffAsync_CompareFlags_AllCombinations(bool hold, bool expiry, string expectedQuery)
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, DiffJson));
        var client = NewClient(handler);
        client.SetToken("T");

        await client.FetchStockDiffAsync("all", hold, expiry);

        Assert.Equal(expectedQuery, handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    // 边界：data 为空数组返回空列表（与 data:null 相区分）
    public async Task FetchStockDiffAsync_EmptyArray_ReturnsEmptyList()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":0,"message":"success","data":[]}""")));
        client.SetToken("T");

        var rows = await client.FetchStockDiffAsync("all", false, false);

        Assert.Empty(rows);
    }

    [Fact]
    // 正常流程：多条记录按服务端顺序保留，条数一致
    public async Task FetchStockDiffAsync_MultipleRows_PreservesOrderAndCount()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":0,"message":"success","data":[{"material_code":"M-1"},{"material_code":"M-2"},{"material_code":"M-3"}]}""")));
        client.SetToken("T");

        var rows = await client.FetchStockDiffAsync("all", false, false);

        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "M-1", "M-2", "M-3" }, rows.Select(r => r.MaterialCode).ToArray());
    }

    [Fact]
    // 回归 Bug#1：message 为 JSON null 且业务码非 0 时，应回退为「错误码」文案，不得漏出 NullReferenceException
    public async Task FetchStockDiffAsync_NullMessage_FallsBackToErrorCode()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":1002,"message":null,"data":null}""")));
        client.SetToken("T");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Equal("获取库存差异失败: 错误码 1002", ex.Message);
    }


    [Fact]
    // 异常：未登录（无令牌）抛 UnauthorizedException 且不发出任何请求
    public async Task FetchStockDiffAsync_NoToken_ThrowsWithoutRequest()
    {
        var called = false;
        var handler = new StubHandler(_ => { called = true; return Json(HttpStatusCode.OK, DiffJson); });
        var client = NewClient(handler);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Equal("请先登录", ex.Message);
        Assert.False(called);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    // 异常：HTTP 401/403 抛 UnauthorizedException 并清空当前令牌
    public async Task FetchStockDiffAsync_AuthStatus_ThrowsAndClearsToken(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => Json(status, "{}"));
        var client = NewClient(handler);
        client.SetToken("T");

        await Assert.ThrowsAsync<UnauthorizedException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Equal("", client.Token);
    }

    [Fact]
    // 边界：旧请求 401 时令牌已被新会话替换，则不误清新令牌
    public async Task FetchStockDiffAsync_StaleUnauthorized_KeepsNewToken()
    {
        ApiClient? client = null;
        var handler = new StubHandler(_ =>
        {
            client!.SetToken("NEW");
            return Json(HttpStatusCode.Unauthorized, "{}");
        });
        client = NewClient(handler);
        client.SetToken("OLD");

        await Assert.ThrowsAsync<UnauthorizedException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Equal("NEW", client.Token);
    }

    [Theory]
    [InlineData(401, "登录已过期")]
    [InlineData(403, "禁止访问")]
    [InlineData(1, "Token 无效")]
    // 异常：业务码 401 或 message 含 token 均按登录失效处理
    public async Task FetchStockDiffAsync_BusinessUnauthorized_ThrowsUnauthorized(int code, string message)
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            $"{{\"code\":{code},\"message\":\"{message}\",\"data\":null}}"));
        var client = NewClient(handler);
        client.SetToken("T");

        await Assert.ThrowsAsync<UnauthorizedException>(() => client.FetchStockDiffAsync("all", false, false));
    }

    [Fact]
    // 异常：HTTP 非 200 抛 ApiException，文案含状态码
    public async Task FetchStockDiffAsync_HttpError_ThrowsApiException()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "boom"));
        var client = NewClient(handler);
        client.SetToken("T");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Contains("HTTP 500", ex.Message);
    }

    [Theory]
    [InlineData("查询参数不合法", "查询参数不合法")]
    [InlineData("", "获取库存差异失败: 错误码 1002")]
    // 异常：业务码非 0 时透出服务端文案，缺失则回退错误码文案
    public async Task FetchStockDiffAsync_NonZeroCode_Throws(string message, string expected)
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            $"{{\"code\":1002,\"message\":\"{message}\",\"data\":null}}"));
        var client = NewClient(handler);
        client.SetToken("T");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Equal(expected, ex.Message);
    }

    [Fact]
    // 异常：非法 JSON 抛 ApiException
    public async Task FetchStockDiffAsync_InvalidJson_ThrowsApiException()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "not-json")));
        client.SetToken("T");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Contains("响应解析失败", ex.Message);
    }

    [Fact]
    // 异常：响应体为 null 字面量抛 ApiException
    public async Task FetchStockDiffAsync_NullBody_ThrowsApiException()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "null")));
        client.SetToken("T");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Contains("响应为空", ex.Message);
    }

    [Fact]
    // 异常：请求超时（非用户取消）包装为含排查建议的 ApiException
    public async Task FetchStockDiffAsync_Timeout_WrapsAsApiExceptionWithHint()
    {
        var client = NewClient(new StubHandler(_ => throw new TaskCanceledException("boom")));
        client.SetToken("T");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Contains("请求超时", ex.Message);
    }

    [Fact]
    // 异常：网络异常包装为 ApiException
    public async Task FetchStockDiffAsync_HttpRequestException_WrapsAsApiException()
    {
        var client = NewClient(new StubHandler(_ => throw new HttpRequestException("发送请求时出错")));
        client.SetToken("T");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Contains("发送请求时出错", ex.Message);
    }

    [Fact]
    // 异常：用户主动取消透传 OperationCanceledException，不包装为 ApiException
    public async Task FetchStockDiffAsync_UserCancel_PropagatesOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        var handler = new StubHandler(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });
        var client = NewClient(handler);
        client.SetToken("T");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.FetchStockDiffAsync("all", false, false, cts.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    // 异常：非法接口地址抛 ApiException 且文案含 URL 排查提示（不发请求）
    public async Task FetchStockDiffAsync_InvalidBaseUrl_ThrowsWithUrlHint(string baseUrl)
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, DiffJson)), baseUrl);
        client.SetToken("T");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Contains("URL错误", ex.Message);
    }

    [Fact]
    // 回归 R3：拉取数据时响应读取步骤抛 IOException（如 HttpIOException）→ 包装为 ApiException，不漏出
    public async Task FetchStockDiffAsync_IOException_WrapsAsApiException()
    {
        // 从 handler 层抛出：真实链路中 HttpIOException 正是在此产生，会原样透传至 ApiClient 的 IOException 分支
        var client = NewClient(new StubHandler(_ => throw new IOException("连接被中断")));
        client.SetToken("T");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Contains("连接被中断", ex.Message);
        Assert.IsType<IOException>(ex.InnerException);
    }

    [Fact]
    // 回归 R4：登录时响应读取步骤抛 IOException → 同样包装为 ApiException
    public async Task LoginAsync_IOException_WrapsAsApiException()
    {
        var client = NewClient(new StubHandler(_ => throw new IOException("连接被中断")));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Contains("连接被中断", ex.Message);
        Assert.IsType<IOException>(ex.InnerException);
    }

    [Fact]
    // 异常：登录响应 message 为 JSON null 且业务码非 0 时回退错误码文案，不漏出 NullReferenceException
    public async Task LoginAsync_NullMessage_FallsBackToErrorCode()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":1001,"message":null,"data":null}""")));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Equal("登录失败: 错误码 1001", ex.Message);
    }

    [Fact]
    // 异常：token 为 JSON null（而非字段缺失）时同样按「未返回token」处理
    public async Task LoginAsync_NullTokenValue_ThrowsApiException()
    {
        var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":0,"message":"success","data":{"token":null}}""")));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("000", "0000"));

        Assert.Contains("未返回token", ex.Message);
    }

    [Fact]
    // 边界：纯空白令牌视为未登录，抛 UnauthorizedException 且不发出请求
    public async Task FetchStockDiffAsync_WhitespaceToken_ThrowsWithoutRequest()
    {
        var called = false;
        var handler = new StubHandler(_ => { called = true; return Json(HttpStatusCode.OK, DiffJson); });
        var client = NewClient(handler);
        client.SetToken("   ");

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => client.FetchStockDiffAsync("all", false, false));

        Assert.Equal("请先登录", ex.Message);
        Assert.False(called);
    }

    [Fact]
    // 安全：warehouse 含查询串分隔符时被转义，不会注入额外查询参数
    public async Task FetchStockDiffAsync_WarehouseWithSpecialChars_IsEscaped()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, DiffJson));
        var client = NewClient(handler);
        client.SetToken("T");

        await client.FetchStockDiffAsync("a&b=c d", false, false);

        var query = handler.LastRequest!.RequestUri!.Query;
        // 仅两个分隔符（compare_hold / compare_expiry），注入的 & 与 = 必须已被转义
        Assert.Equal(2, query.Count(c => c == '&'));
        Assert.DoesNotContain("b=c", query);
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