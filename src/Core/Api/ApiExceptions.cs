// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: API 层异常类型定义，区分登录失效与一般接口错误，供 UI 层分别处理。

namespace StockDiff.Core.Api;

// 登录失效异常：令牌过期或服务端返回 401/403 时抛出，UI 据此跳回登录页
public sealed class UnauthorizedException : Exception
{
    // 默认文案即面向用户的提示，调用方一般无需传参
    public UnauthorizedException(string message = "登录已过期，请重新登录") : base(message) { }
}

// 一般接口错误：HTTP 非 200、业务码非 0、响应解析失败、网络异常等，Message 即可直接展示
public sealed class ApiException : Exception
{
    // inner 保留原始异常，便于日志追溯根因
    public ApiException(string message, Exception? inner = null) : base(message, inner) { }
}