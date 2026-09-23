// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: AppSettings 持久化单元测试，通过 OverrideConfigDir 重定向到临时目录，
//       覆盖落盘回读、原子写无残留、环境变量优先级、损坏文件与不可写路径降级等分支；
//       并覆盖 AppSettingsStore 适配器委托、BaseUrl 属性写入即落盘、超长地址、null 配置与并发写入一致性。

using System.Text.Json;
using StockDiff.App.Settings;
using StockDiff.Core.Config;
using Xunit;

namespace App.Tests;

// AppSettings 为静态类，测试隔离靠「每实例一个临时目录」，因此全部用例集中在单个测试类内顺序执行
public sealed class AppSettingsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "kc-stock-diff-tests", Guid.NewGuid().ToString("N"));

    private readonly string? _originalEnv = Environment.GetEnvironmentVariable(AppConfig.BaseUrlEnvVar);

    // 每个用例开始：建独立临时目录、清空环境变量、重定向配置目录，保证与真实配置完全隔离
    public AppSettingsTests()
    {
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable(AppConfig.BaseUrlEnvVar, null);
        AppSettings.OverrideConfigDir(_tempDir);
    }

    // 每个用例结束：恢复默认目录、还原环境变量、删除临时目录
    public void Dispose()
    {
        AppSettings.OverrideConfigDir(null);
        Environment.SetEnvironmentVariable(AppConfig.BaseUrlEnvVar, _originalEnv);

        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // 清理失败不影响断言结果
        }
    }

    // 临时目录下的配置文件路径
    private string SettingsFile => Path.Combine(_tempDir, "settings.json");

    [Fact]
    // 正常流程：落盘后重新 Load 能回读，等价于「改地址 → 重启后仍生效」
    public void TrySetBaseUrl_PersistsAndSurvivesReload()
    {
        Assert.True(AppSettings.TrySetBaseUrl("http://10.0.0.1:8080"));

        AppSettings.Load();

        Assert.Equal("http://10.0.0.1:8080", AppSettings.BaseUrl);
    }

    [Fact]
    // 正常流程：原子写只保留最终文件，不残留 .tmp 中间文件
    public void Save_LeavesNoTempFile()
    {
        AppSettings.TrySetBaseUrl("http://10.0.0.1:8080");

        Assert.True(File.Exists(SettingsFile));
        Assert.False(File.Exists(SettingsFile + ".tmp"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // 边界：既无持久化文件、环境变量也为空白时回退默认地址
    public void Load_NoPersistedNoEnv_FallsBackToDefault(string? env)
    {
        Environment.SetEnvironmentVariable(AppConfig.BaseUrlEnvVar, env);

        AppSettings.Load();

        Assert.Equal(AppConfig.DefaultBaseUrl, AppSettings.BaseUrl);
    }

    [Fact]
    // 正常流程：无持久化文件时采用环境变量作为首次默认值
    public void Load_NoPersisted_UsesEnvironment()
    {
        Environment.SetEnvironmentVariable(AppConfig.BaseUrlEnvVar, "http://env-host:9000");

        AppSettings.Load();

        Assert.Equal("http://env-host:9000", AppSettings.BaseUrl);
    }

    [Fact]
    // 正常流程：本地持久化（用户显式设置）优先于环境变量
    public void Load_PersistedWinsOverEnvironment()
    {
        AppSettings.TrySetBaseUrl("http://persisted:1");
        Environment.SetEnvironmentVariable(AppConfig.BaseUrlEnvVar, "http://env:1");

        AppSettings.Load();

        Assert.Equal("http://persisted:1", AppSettings.BaseUrl);
    }

    [Theory]
    [InlineData("{ not-json")]
    [InlineData("")]
    [InlineData("""{"base_url":123}""")]
    // 异常/降级：配置文件损坏（非法 JSON / 空文件 / 类型不符）时静默回退，不抛异常
    public void Load_CorruptedFile_DegradesToFallback(string content)
    {
        File.WriteAllText(SettingsFile, content);

        AppSettings.Load();

        Assert.Equal(AppConfig.DefaultBaseUrl, AppSettings.BaseUrl);
    }

    [Fact]
    // 异常/降级：落盘路径不可用（此处指向一个已存在的文件，CreateDirectory 会抛 IOException）
    // → Save 返回 false，但内存中的地址仍生效，供 UI 提示用户
    public void TrySetBaseUrl_UnwritablePath_ReportsFalseButAppliesInMemory()
    {
        var blocked = Path.Combine(_tempDir, "blocked");
        File.WriteAllText(blocked, "x");
        AppSettings.OverrideConfigDir(blocked);

        Assert.False(AppSettings.TrySetBaseUrl("http://10.0.0.1:8080"));
        Assert.Equal("http://10.0.0.1:8080", AppSettings.BaseUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // 边界：空白入参归一为默认地址后再落盘
    public void TrySetBaseUrl_Blank_NormalizesToDefault(string? raw)
    {
        Assert.True(AppSettings.TrySetBaseUrl(raw!));

        AppSettings.Load();

        Assert.Equal(AppConfig.DefaultBaseUrl, AppSettings.BaseUrl);
    }

    // ---------- BaseUrl 属性 setter：写入即落盘 ----------

    [Fact]
    // 正常流程：属性 setter 归一化后立即落盘，重启（仅从磁盘回读）后地址仍生效
    public void BaseUrlPropertySetter_PersistsImmediately()
    {
        AppSettings.BaseUrl = "  http://10.0.0.1:8080  ";

        Assert.Equal("http://10.0.0.1:8080", AppSettings.BaseUrl);

        AppSettings.Load();

        Assert.Equal("http://10.0.0.1:8080", AppSettings.BaseUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // 边界：属性 setter 收到空白 → 归一化为默认地址并落盘（读取侧同样兜底）
    public void BaseUrlPropertySetter_Blank_NormalizesToDefault(string? raw)
    {
        AppSettings.BaseUrl = raw!;

        Assert.Equal(AppConfig.DefaultBaseUrl, AppSettings.BaseUrl);

        AppSettings.Load();

        Assert.Equal(AppConfig.DefaultBaseUrl, AppSettings.BaseUrl);
    }

    [Fact]
    // 极端值：超长地址（约 2KB）落盘后完整回读，不截断、不丢字符
    public void BaseUrlPropertySetter_OverlongUrl_RoundTrips()
    {
        var url = "http://10.0.0.1:7880/" + new string('a', 1980);

        AppSettings.BaseUrl = url;

        AppSettings.Load();

        Assert.Equal(url, AppSettings.BaseUrl);
    }

    [Fact]
    // 边界：配置文件里 base_url 显式为 null（而非缺字段）→ 回退默认地址
    public void Load_NullBaseUrlInFile_FallsBackToDefault()
    {
        File.WriteAllText(SettingsFile, """{"base_url":null}""");

        AppSettings.Load();

        Assert.Equal(AppConfig.DefaultBaseUrl, AppSettings.BaseUrl);
    }

    [Fact]
    // 并发：64 线程同时写入不同地址 → 内部锁 + 原子写保证最终磁盘文件是完整合法 JSON，
    // 且与内存值一致，不会出现半截文件或交叉写入
    public void TrySetBaseUrl_ConcurrentWrites_LeaveConsistentFile()
    {
        var candidates = Enumerable.Range(0, 64).Select(i => $"http://10.0.0.{i}:{8000 + i}").ToArray();

        Parallel.ForEach(candidates, url => AppSettings.TrySetBaseUrl(url));

        var inMemory = AppSettings.BaseUrl;
        Assert.Contains(inMemory, candidates);

        using var doc = JsonDocument.Parse(File.ReadAllText(SettingsFile));
        Assert.Equal(inMemory, doc.RootElement.GetProperty("base_url").GetString());
    }

    // ---------- AppSettingsStore：Core 端口 IBaseUrlStore 的 App 层适配器 ----------

    [Fact]
    // 正常流程：Save 落盘、Load 回读一致（适配器正确桥接到 AppSettings）
    public void Store_SaveThenLoad_RoundTrips()
    {
        var store = new AppSettingsStore();

        Assert.True(store.Save("http://10.0.0.1:8080"));

        AppSettings.Load();

        Assert.Equal("http://10.0.0.1:8080", store.Load());
    }

    [Fact]
    // 边界：磁盘无任何配置时 Load 永不返回 null（兜底默认地址，调用方无需判空）
    public void Store_Load_NoPersistedFile_ReturnsDefault()
    {
        var store = new AppSettingsStore();

        // AppSettings 为静态类，内存值可能残留自其它用例；先 Load 以「空目录 + 无环境变量」重建状态
        AppSettings.Load();

        Assert.Equal(AppConfig.DefaultBaseUrl, store.Load());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    // 边界：Save 空白 → 归一化为默认地址后落盘并返回 true
    public void Store_Save_Blank_NormalizesToDefault(string? raw)
    {
        var store = new AppSettingsStore();

        Assert.True(store.Save(raw!));

        AppSettings.Load();

        Assert.Equal(AppConfig.DefaultBaseUrl, store.Load());
    }

    [Fact]
    // 异常：落盘路径不可用 → Save 返回 false 供 UI 提示，内存值仍生效
    public void Store_Save_UnwritablePath_ReportsFalse()
    {
        var blocked = Path.Combine(_tempDir, "blocked");
        File.WriteAllText(blocked, "x");
        AppSettings.OverrideConfigDir(blocked);
        var store = new AppSettingsStore();

        Assert.False(store.Save("http://10.0.0.1:8080"));
        Assert.Equal("http://10.0.0.1:8080", store.Load());
    }
}