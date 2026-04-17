using Microsoft.Extensions.Primitives;

namespace AetherCore.WebSockets
{
    public interface IWebSocketMiddleware
    {
        Task OnConnectedAsync(WebSocketContext ctx, Func<Task> next);
        Task OnDisconnectedAsync(WebSocketContext ctx, Func<Task> next);

        Task OnTextAsync(WebSocketContext ctx, string text, Func<Task> next);
        Task OnBinaryAsync(WebSocketContext ctx, byte[] bytes, Func<Task> next);
    }

}
