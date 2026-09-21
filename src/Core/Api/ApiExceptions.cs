// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: API 层异常类型定义，区分登录失效与一般接口错误，供 UI 层分别处理。

namespace StockDiff.Core.Api;

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "登录已过期，请重新登录") : base(message) { }
}

public sealed class ApiException : Exception
{
    public ApiException(string message, Exception? inner = null) : base(message, inner) { }
}