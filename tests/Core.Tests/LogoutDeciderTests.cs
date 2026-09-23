// 创建者: PlatyPus
// 创建时间: 2026-09-23
// 作用: LogoutDecider 单元测试，锁定退出登录决策的正常流程、空消息兜底边界与非鉴权异常不退出、入参为空抛错。

using StockDiff.Core.Api;
using StockDiff.Core.Session;
using Xunit;

namespace Core.Tests;

public sealed class LogoutDeciderTests
{
    [Fact]
    // 正常流程：默认 UnauthorizedException 触发退出，文案为默认过期提示
    public void FromException_UnauthorizedWithDefaultMessage_ShouldLogoutWithExpiredMessage()
    {
        var outcome = LogoutDecider.FromException(new UnauthorizedException());

        Assert.True(outcome.ShouldLogout);
        Assert.Equal(LogoutDecider.DefaultExpiredMessage, outcome.Message);
    }

    [Fact]
    // 正常流程：带自定义文案的 UnauthorizedException 透传该文案
    public void FromException_UnauthorizedWithCustomMessage_ShouldLogoutWithCustomMessage()
    {
        var outcome = LogoutDecider.FromException(new UnauthorizedException("token 已失效"));

        Assert.True(outcome.ShouldLogout);
        Assert.Equal("token 已失效", outcome.Message);
    }

    [Fact]
    // 正常流程：设置保存后退出，文案为固定设置已保存提示
    public void FromSettingsSaved_ShouldLogoutWithSettingsMessage()
    {
        var outcome = LogoutDecider.FromSettingsSaved();

        Assert.True(outcome.ShouldLogout);
        Assert.Equal(LogoutDecider.SettingsSavedMessage, outcome.Message);
    }

    [Fact]
    // 正常流程：手动退出登录，文案为固定手动退出提示
    public void FromManualLogout_ShouldLogoutWithManualMessage()
    {
        var outcome = LogoutDecider.FromManualLogout();

        Assert.True(outcome.ShouldLogout);
        Assert.Equal(LogoutDecider.ManualLogoutMessage, outcome.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    // 边界：UnauthorizedException 显式传空串 / 纯空白 → 兜底默认文案
    public void FromException_UnauthorizedWithBlankMessage_FallsBackToDefault(string message)
    {
        var outcome = LogoutDecider.FromException(new UnauthorizedException(message));

        Assert.True(outcome.ShouldLogout);
        Assert.Equal(LogoutDecider.DefaultExpiredMessage, outcome.Message);
    }

    [Fact]
    // 异常分支：ApiException 不触发退出，文案为空（保持旧数据可导出，不跳登录页）
    public void FromException_ApiException_DoesNotLogout()
    {
        var outcome = LogoutDecider.FromException(new ApiException("网络错误"));

        Assert.False(outcome.ShouldLogout);
        Assert.Equal("", outcome.Message);
    }

    [Fact]
    // 异常分支：非鉴权异常不触发退出
    public void FromException_GeneralException_DoesNotLogout()
    {
        var outcome = LogoutDecider.FromException(new InvalidOperationException("非鉴权"));

        Assert.False(outcome.ShouldLogout);
        Assert.Equal("", outcome.Message);
    }

    [Fact]
    // 异常分支：入参为 null 抛 ArgumentNullException
    public void FromException_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LogoutDecider.FromException(null!));
    }
}
