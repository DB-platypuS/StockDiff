// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: AppSettings 持久化单元测试，通过 OverrideConfigDir 重定向到临时目录，
//       覆盖落盘回读、原子写无残留、环境变量优先级、损坏文件与不可写路径降级等分支。

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
}