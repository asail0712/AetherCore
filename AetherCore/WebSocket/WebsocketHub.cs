using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;

namespace AetherCore.WebSockets
{
    // 連線管理 + 簡單路由

    public class WebsocketHub
    {
        private readonly List<IWebSocketMiddleware> _middlewares                    = new();
        private readonly ConcurrentDictionary<string, WebSocket> _peers             = new();        
        private readonly ConcurrentDictionary<WebSocket, SemaphoreSlim> _sendLocks  = new(); // 每個 ws 一把鎖，避免同 socket 同時 Send
        private IReadOnlyDictionary<string, StringValues> headerDict                = null;

        public async Task RunAsync(WebSocketIdentity identity, WebSocket socket, IReadOnlyDictionary<string, StringValues> headerDict, CancellationToken ct)
        {
            this.headerDict = headerDict;

            await AddAsync(identity, socket, headerDict, ct);

            var buffer = new byte[64 * 1024];

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var (msgType, payload) = await ReceiveFullMessageAsync(socket, buffer, ct);

                    if (msgType == null) break;

                    if (msgType == WebSocketMessageType.Close)
                        break;

                    if (msgType == WebSocketMessageType.Text)
                    {
                        var msg = Encoding.UTF8.GetString(payload);
                        await HandleTextAsync(identity, msg, ct);
                    }
                    else if (msgType == WebSocketMessageType.Binary)
                    {
                        await HandleBinaryAsync(identity, payload, ct);
                    }
                }
            }
            catch (OperationCanceledException) { /* client aborted */ }
            catch (WebSocketException) { /* network error */ }
            finally
            {
                await RemoveAsync(identity, socket);
                try
                {
                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct);
                }
                catch { /* ignore */ }
            }
        }

        private async Task<bool> AddAsync(WebSocketIdentity identity, WebSocket ws, IReadOnlyDictionary<string, StringValues> headerDict, CancellationToken ct)
        {
            bool replaced = false;

            // 若已有舊連線 → 踢掉
            if (_peers.TryGetValue(identity.UserId, out var oldWs) && !ReferenceEquals(oldWs, ws))
            {
                replaced = true;

                await SafeCloseAsync(oldWs, WebSocketCloseStatus.PolicyViolation, "replaced by new connection");
                await RemoveAsync(identity, oldWs);
            }

            _peers[identity.UserId] = ws;
            _sendLocks.TryAdd(ws, new SemaphoreSlim(1, 1));

            await OnAddAsync(identity, headerDict, ct);

            return replaced;
        }

        private async Task<bool> RemoveAsync(WebSocketIdentity identity, WebSocket ws)
        {
            // 移除送鎖 送進來的ws 不管有沒有在_peers裡面都移除
            // 1. 在_peers裡面代表正常斷線
            // 2. 不在_peers裡面代表被新的連線取代 也不該存在
            if(_sendLocks.TryRemove(ws, out var gate))
            {
                gate.Dispose();
            }

            if (_peers.TryGetValue(identity.UserId, out var curr) && ReferenceEquals(curr, ws))
            {
                bool bRemoved = _peers.TryRemove(identity.UserId, out _);

                if (bRemoved)
                    await OnRemoveAsync(identity, CancellationToken.None);

                return bRemoved;
            }

            return false;
        }

        /*==========================================
         * for override
         * ========================================*/
        protected virtual Task OnAddCoreAsync(WebSocketIdentity identity, IReadOnlyDictionary<string, StringValues> query, CancellationToken ct)
        {
            // for override
            return Task.CompletedTask;
        }

        protected virtual Task OnRemoveCoreAsync(WebSocketIdentity identity, CancellationToken ct)
        {
            // for override
            return Task.CompletedTask;
        }

        protected virtual Task HandleTextCoreAsync(WebSocketIdentity identity, string json, CancellationToken ct)
        {
            // for override
            return Task.CompletedTask;
        }

        protected virtual Task HandleBinaryCoreAsync(WebSocketIdentity identity, byte[] bytes, CancellationToken ct)
        {
            // for override
            return Task.CompletedTask;
        }

        /*==========================================
         * for middleware
         * ========================================*/
        public WebsocketHub Use(IWebSocketMiddleware mw)
        {
            _middlewares.Add(mw);
            return this;
        }

        protected Task OnAddAsync(WebSocketIdentity identity, IReadOnlyDictionary<string, StringValues> headers, CancellationToken ct)
        {
            var ctx = new WebSocketContext(
                identity,
                headers: headerDict, // 連線時可存起來，或存在 peers dict
                ct,
                sendJsonAsync: obj => SendAsync(identity.UserId, obj, ct),
                sendBinaryAsync: bytes => SendBinaryAsync(identity.UserId, bytes, ct)
            );

            Task Invoke(int idx)
            {
                if (idx >= _middlewares.Count)
                    return OnAddCoreAsync(identity, headers, ct);

                return _middlewares[idx].OnConnectedAsync(ctx, () => Invoke(idx + 1));
            }

            return Invoke(0);
        }

        protected Task OnRemoveAsync(WebSocketIdentity identity, CancellationToken ct)
        {
            var ctx = new WebSocketContext(
                identity,
                headers: headerDict, // 連線時可存起來，或存在 peers dict
                ct,
                sendJsonAsync: obj => SendAsync(identity.UserId, obj, ct),
                sendBinaryAsync: bytes => SendBinaryAsync(identity.UserId, bytes, ct)
            );

            Task Invoke(int idx)
            {
                if (idx >= _middlewares.Count)
                    return OnRemoveCoreAsync(identity, ct);

                return _middlewares[idx].OnDisconnectedAsync(ctx, () => Invoke(idx + 1));
            }

            return Invoke(0);
        }

        protected Task HandleTextAsync(WebSocketIdentity identity, string json, CancellationToken ct)
        {
            var ctx = new WebSocketContext(
                identity,
                headers: headerDict, // 連線時可存起來，或存在 peers dict
                ct,
                sendJsonAsync: obj => SendAsync(identity.UserId, obj, ct),
                sendBinaryAsync: bytes => SendBinaryAsync(identity.UserId, bytes, ct)
            );

            Task Invoke(int idx)
            {
                if (idx >= _middlewares.Count)
                    return HandleTextCoreAsync(identity, json, ct);

                return _middlewares[idx].OnTextAsync(ctx, json, () => Invoke(idx + 1));
            }

            return Invoke(0);
        }

        protected Task HandleBinaryAsync(WebSocketIdentity identity, byte[] bytes, CancellationToken ct)
        {
            var ctx = new WebSocketContext(
                identity,
                headers: headerDict, // 連線時可存起來，或存在 peers dict
                ct,
                sendJsonAsync: obj => SendAsync(identity.UserId, obj, ct),
                sendBinaryAsync: bytes => SendBinaryAsync(identity.UserId, bytes, ct)
            );

            Task Invoke(int idx)
            {
                if (idx >= _middlewares.Count)
                    return HandleBinaryCoreAsync(identity, bytes, ct);

                return _middlewares[idx].OnBinaryAsync(ctx, bytes, () => Invoke(idx + 1));
            }

            return Invoke(0);
        }

        /*==========================================
         * for send msg
         * ========================================*/
        protected Task SendAsync(string uid, object obj, CancellationToken ct)
        {
            if (!TryGetValue(uid, out WebSocket ws))
                return Task.CompletedTask;

            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));

            return TrySendAsync(ws, bytes, WebSocketMessageType.Text, ct);
        }

        protected Task SendBinaryAsync(string uid, byte[] bytes, CancellationToken ct)
        {
            if (!TryGetValue(uid, out WebSocket ws))
                return Task.CompletedTask;

            return TrySendAsync(ws, bytes, WebSocketMessageType.Binary, ct);
        }

        /*==========================================
         * for internal
         * ========================================*/

        protected async Task BroadcastAsync(object obj, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));

            foreach (var (_, ws) in _peers)
                if (ws.State == WebSocketState.Open)
                    await TrySendAsync(ws, bytes, WebSocketMessageType.Text, ct);
        }

        protected async Task BroadcastBinaryAsync(byte[] bytes, CancellationToken ct)
        {
            foreach (var (_, ws) in _peers)
                if (ws.State == WebSocketState.Open)
                    await TrySendAsync(ws, bytes, WebSocketMessageType.Binary, ct);
        }        

        protected bool TryGetValue(string fromUid, out WebSocket ws)
        {
            return _peers.TryGetValue(fromUid, out ws);
        }

        private async Task TrySendAsync(WebSocket ws, byte[] bytes, WebSocketMessageType type, CancellationToken ct)
        {
            if (ws.State != WebSocketState.Open) return;

            if (!_sendLocks.TryGetValue(ws, out var gate))
                return;
            
            try
            {
                await gate.WaitAsync(ct);

                if (ws.State == WebSocketState.Open)
                    await ws.SendAsync(bytes, type, endOfMessage: true, cancellationToken: ct);
            }
            catch
            {
                // 送失敗通常代表斷線：清掉
                try { ws.Abort(); } catch { }
            }
            finally
            {
                gate.Release();
            }
        }

        private static async Task SafeCloseAsync(WebSocket ws, WebSocketCloseStatus status, string reason)
        {
            try
            {
                if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                    await ws.CloseAsync(status, reason, CancellationToken.None);
                else
                    ws.Abort();
            }
            catch
            {
                try { ws.Abort(); } catch { }
            }
        }

        private static async Task<(WebSocketMessageType? type, byte[] payload)> ReceiveFullMessageAsync(
            WebSocket socket, byte[] buffer, CancellationToken ct)
        {
            using var ms = new MemoryStream();

            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    return (WebSocketMessageType.Close, Array.Empty<byte>());

                if (result.Count > 0)
                    ms.Write(buffer, 0, result.Count);

                if (result.EndOfMessage)
                    return (result.MessageType, ms.ToArray());
            }
        }
    }
}
