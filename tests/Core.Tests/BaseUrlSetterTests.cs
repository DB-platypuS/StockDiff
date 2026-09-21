// 创建者: PlatyPus
// 创建时间: 2026-09-21
// 作用: BaseUrlSetter 接口地址设置与持久化单元测试，覆盖归一化落盘、清空令牌、空白兜底、非法地址与空参数。

using System.Net;
using StockDiff.Core.Api;
using StockDiff.Core.Config;
using Xunit;

namespace Core.Tests;

public sealed class BaseUrlSetterTests
{
    private const string DefaultUrl = "http://127.0.0.1:7880";

    // 测试辅助：构造待测客户端与内存版假存储
    private static (ApiClient Client, RecordingStore Store) NewPair(string baseUrl = DefaultUrl) =>
        (new ApiClient(baseUrl, new StubHandler()), new RecordingStore());

    [Fact]
    // 正常流程：合法地址去空白后落盘、写入客户端并返回归一化值
    public void SetBaseUrl_Valid_TrimPersistAndApply()
    {
        var (client, store) = NewPair();

        var applied = BaseUrlSetter.SetBaseUrl(client, store, "  http://10.0.0.1:8080  ");

        Assert.Equal("http://10.0.0.1:8080", applied);
        Assert.Equal("http://10.0.0.1:8080", client.BaseUrl);
        Assert.Equal("http://10.0.0.1:8080", store.Current);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    // 正常流程：地址变更后必须清空登录令牌
    public void SetBaseUrl_ClearsExistingToken()
    {
        var (client, store) = NewPair();
        client.SetToken("T-1");

        BaseUrlSetter.SetBaseUrl(client, store, "http://10.0.0.1:8080");

        Assert.Equal("", client.Token);
    }

    [Fact]
    // 正常流程：落盘后可回读，等价于重启后地址仍生效
    public void SetBaseUrl_Persists_ReadBackAfterReload()
    {
        var (client, store) = NewPair();

        BaseUrlSetter.SetBaseUrl(client, store, "http://192.168.13.8:7880/");

        Assert.Equal("http://192.168.13.8:7880/", store.Load());
    }

    [Fact]
    // 边界：末尾斜杠原样保留，交由 ApiClient 拼接时处理
    public void SetBaseUrl_TrailingSlashPreserved()
    {
        var (client, store) = NewPair();

        Assert.Equal("http://10.0.0.1:8080/",
            BaseUrlSetter.SetBaseUrl(client, store, "http://10.0.0.1:8080/"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // 边界：空白输入回退默认地址并落盘
    public void SetBaseUrl_Blank_FallsBackToDefaultAndPersists(string? raw)
    {
        var (client, store) = NewPair("http://10.0.0.1:8080");

        var applied = BaseUrlSetter.SetBaseUrl(client, store, raw);

        Assert.Equal(DefaultUrl, applied);
        Assert.Equal(DefaultUrl, client.BaseUrl);
        Assert.Equal(DefaultUrl, store.Current);
    }

    [Fact]
    // 边界：https 地址合法
    public void SetBaseUrl_HttpsAccepted() =>
        Assert.Equal("https://api.example.com",
            BaseUrlSetter.SetBaseUrl(NewPair().Client, new RecordingStore(), "https://api.example.com"));

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("192.168.13.8:7880")]
    [InlineData("ftp://192.168.13.8:7880")]
    [InlineData("http://")]
    // 异常：非法地址抛 ArgumentException，且不落盘、不改写客户端、不清令牌
    public void SetBaseUrl_Invalid_ThrowsWithoutSideEffects(string raw)
    {
        var (client, store) = NewPair();
        client.SetToken("T-keep");

        var ex = Assert.Throws<ArgumentException>(() => BaseUrlSetter.SetBaseUrl(client, store, raw));

        Assert.Contains("格式不正确", ex.Message);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(DefaultUrl, client.BaseUrl);
        Assert.Equal("T-keep", client.Token);
    }

    [Fact]
    // 异常：client 为 null 抛 ArgumentNullException
    public void SetBaseUrl_NullClient_Throws() =>
        Assert.Throws<ArgumentNullException>(
            () => BaseUrlSetter.SetBaseUrl(null!, new RecordingStore(), "http://a"));

    [Fact]
    // 异常：store 为 null 抛 ArgumentNullException
    public void SetBaseUrl_NullStore_Throws() =>
        Assert.Throws<ArgumentNullException>(
            () => BaseUrlSetter.SetBaseUrl(NewPair().Client, null!, "http://a"));

    [Fact]
    // 边界：TryNormalize 合法返回 true 且 error 为空；非法返回 false 并带文案
    public void TryNormalize_ReportsValidityAndError()
    {
        Assert.True(BaseUrlSetter.TryNormalize("http://a:1", out var ok, out var noError));
        Assert.Equal("http://a:1", ok);
        Assert.Equal("", noError);

        Assert.False(BaseUrlSetter.TryNormalize("nope", out _, out var error));
        Assert.Contains("格式不正确", error);
    }

    [Fact]
    // 边界：scheme 大写仍合法，且归一化保留用户原始文本大小写
    public void SetBaseUrl_UppercaseScheme_Accepted()
    {
        var (client, store) = NewPair();

        var applied = BaseUrlSetter.SetBaseUrl(client, store, "HTTP://10.0.0.1:8080");

        Assert.Equal("HTTP://10.0.0.1:8080", applied);
        Assert.Equal("HTTP://10.0.0.1:8080", client.BaseUrl);
    }

    [Fact]
    // 边界：端口超出 0-65535 视为非法地址，且不落盘
    public void SetBaseUrl_PortOutOfRange_Throws()
    {
        var (client, store) = NewPair();

        Assert.Throws<ArgumentException>(() =>
            BaseUrlSetter.SetBaseUrl(client, store, "http://10.0.0.1:99999"));
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    // 边界：超长非法输入（无 scheme）被拒绝且不落盘
    public void SetBaseUrl_OverlongGarbage_Throws()
    {
        var (client, store) = NewPair();

        Assert.Throws<ArgumentException>(() =>
            BaseUrlSetter.SetBaseUrl(client, store, new string('a', 4096)));
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    // 边界：连续设置两次以最后一次为准，并累计落盘两次
    public void SetBaseUrl_CalledTwice_LastWins()
    {
        var (client, store) = NewPair();

        BaseUrlSetter.SetBaseUrl(client, store, "http://10.0.0.1:8080");
        var applied = BaseUrlSetter.SetBaseUrl(client, store, "http://10.0.0.2:9090");

        Assert.Equal("http://10.0.0.2:9090", applied);
        Assert.Equal("http://10.0.0.2:9090", client.BaseUrl);
        Assert.Equal(2, store.SaveCount);
    }

    [Fact]
    // 边界：空白输入回退默认地址，并同样清空已登录令牌
    public void SetBaseUrl_Blank_AlsoClearsToken()
    {
        var (client, store) = NewPair();
        client.SetToken("T-old");

        BaseUrlSetter.SetBaseUrl(client, store, "   ");

        Assert.Equal("", client.Token);
    }

    [Fact]
    // P0-1：落盘失败时地址仍生效，但 persisted 返回 false 供 UI 提示用户
    public void SetBaseUrl_PersistFails_AppliesButReportsFalse()
    {
        var client = new ApiClient(DefaultUrl, new StubHandler());

        var applied = BaseUrlSetter.SetBaseUrl(client, new FailingStore(), "http://10.0.0.1:8080", out var persisted);

        Assert.Equal("http://10.0.0.1:8080", applied);
        Assert.Equal("http://10.0.0.1:8080", client.BaseUrl);
        Assert.False(persisted);
    }

    [Fact]
    // P0-1：落盘成功时 persisted 返回 true
    public void SetBaseUrl_PersistSucceeds_ReportsTrue()
    {
        var (client, store) = NewPair();

        BaseUrlSetter.SetBaseUrl(client, store, "http://10.0.0.1:8080", out var persisted);

        Assert.True(persisted);
    }

    [Theory]
    [InlineData("http://persisted:1", "http://env:1", "http://persisted:1")]
    [InlineData("   ", "http://env:1", "http://env:1")]
    [InlineData(null, "http://env:1", "http://env:1")]
    [InlineData("  http://persisted:1  ", null, "http://persisted:1")]
    [InlineData("", "   ", null)]
    [InlineData(null, null, null)]
    // P0-2：启动地址优先级 本地持久化 > 环境变量 > null（由调用方兜底默认地址）
    public void ResolveStartupBaseUrl_PrefersPersistedOverEnvironment(string? persisted, string? fromEnv, string? expected) =>
        Assert.Equal(expected, BaseUrlSetter.ResolveStartupBaseUrl(persisted, fromEnv));

    // 测试替身：内存版地址存储，记录落盘次数与最近一次值
    private sealed class RecordingStore : IBaseUrlStore
    {
        public int SaveCount { get; private set; }
        public string? Current { get; private set; }
        public string? Load() => Current;
        public bool Save(string baseUrl) { SaveCount++; Current = baseUrl; return true; }
    }

    // 测试替身：模拟落盘失败的存储（磁盘只读 / 无权限）
    private sealed class FailingStore : IBaseUrlStore
    {
        public string? Load() => null;
        public bool Save(string baseUrl) => false;
    }

    // 测试替身：不发起真实网络请求（本模块不使用网络）
    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
