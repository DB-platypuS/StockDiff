// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 将网络层异常转换为带排查建议的用户可读文案，区分用户主动取消与请求超时。

using System.Net.Sockets;

namespace StockDiff.Core.Api;

public static class NetworkErrorMapper
{
    public static string Map(Exception ex, bool userCancelled = false)
    {
        var text = ex.Message;

        if (ex is UriFormatException)
        {
            return "💡 URL错误，请检查接口地址配置";
        }

        if (ex is TaskCanceledException or OperationCanceledException or TimeoutException)
        {
            return userCancelled
                ? text
                : text + "\n💡 请求超时，请检查：1.网络连接 2.服务器地址 3.服务器是否启动 4.防火墙";
        }

        var socket = FindSocket(ex);
        if (socket is null)
        {
            return text;
        }

        return socket.SocketErrorCode switch
        {
            SocketError.ConnectionRefused
                => text + "\n💡 连接被拒绝，请检查：1.服务器是否启动 2.端口是否正确 3.服务是否监听",
            SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain
                => text + "\n💡 无法连接到服务器，请检查：1.地址 2.网络 3.服务器是否启动",
            _ => text
        };
    }

    private static SocketException? FindSocket(Exception? ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is SocketException socket)
            {
                return socket;
            }
        }

        return null;
    }
}