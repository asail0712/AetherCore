using Microsoft.Extensions.Primitives;

namespace AetherCore.WebSockets
{
    public sealed class WebSocketContext
    {
        public WebSocketIdentity Identity { get; }
        public IReadOnlyDictionary<string, StringValues> Headers { get; }
        public CancellationToken CancellationToken { get; }

        public Func<object, Task> SendJsonAsync { get; }
        public Func<byte[], Task> SendBinaryAsync { get; }

        public WebSocketContext(
            WebSocketIdentity identity,
            IReadOnlyDictionary<string, StringValues> headers,
            CancellationToken ct,
            Func<object, Task> sendJsonAsync,
            Func<byte[], Task> sendBinaryAsync)
        {
            Identity            = identity;
            Headers             = headers;
            CancellationToken   = ct;
            SendJsonAsync       = sendJsonAsync;
            SendBinaryAsync     = sendBinaryAsync;
        }
    }

}
