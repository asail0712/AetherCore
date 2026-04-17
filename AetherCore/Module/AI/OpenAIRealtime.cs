using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using AetherCore.Module.Interface;

namespace AetherCore.Module.AI
{   
    /// <summary>
    /// Refactored & trimmed version based on user's original file.
    /// Focus: clear responsibilities, minimal state, safe defaults, and readable flow.
    /// </summary>
    public class OpenAIRealtime : IAISpeech, IDisposable
    {
        // ===============================
        // Config (Inspector)
        // ===============================
        public string openAIApiKey      = string.Empty;
        public string model             = "gpt-4o-mini-realtime-preview";
        public string voice             = "alloy";
        public string basicInstructions = "You are a helpful, concise voice assistant.";
        public bool bAutoCreateResponse = false;
        public bool bSendDebugInfo      = false;
        public string SetInstructions { get => basicInstructions; set => basicInstructions = value; }

        // ===============================
        // Internals - WS
        // ===============================
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Uri _uri;
        private volatile bool bConnected;
        private readonly byte[] _recvBuffer             = new byte[1 << 16];            // buffers 64KB    
        private volatile bool bResponseInFlight;                                        // response lifecycle (simple)
        private volatile bool _commitInFlight;
        private TimeSpan receiveIdleTimeout             = TimeSpan.FromSeconds(180);    // 判定是否為殭屍連線

        // 保證 SendAsync 串行化
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        // 把背景執行緒事件丟回主執行緒
        private readonly ConcurrentQueue<Action> _mainThreadActions = new();

        // life cycle
        private bool bDisposed = false;
        private Task recvTask;

        // 當前response類別欄位
        private string _activeAssistantItemId;
        private int? _activeAudioContentIndex;
        private string _squelchItemId; // 要丟棄的舊 item（本地消音用）

        // events
        public event Action OnResposeStart;
        public event Action OnResposeFinish;
        public event Action<string> OnUserTranscriptDelta;
        public event Action<string> OnUserTranscriptDone;
        public event Action<string> OnAssistantTextDelta;
        public event Action<string> OnAssistantTextDone;
        public event Action<byte[]> OnAssistantAudioDelta;
        public event Action OnAssistantAudioDone;
        public event Action<AIErrorType, string> OnError;
        public event Action<string> OnLoggingDone; // 純資訊用（可選）
        private bool bEventAsync = false;

        // ===============================
        // lifecycle
        // ===============================    
        public OpenAIRealtime(string openAIApiKey, string model, string voice, string basicInstructions, bool bAutoCreateResponse, bool bEventAsync = false)
        {
            this.openAIApiKey           = openAIApiKey;
            this.model                  = model;
            this.voice                  = voice;
            this.basicInstructions      = basicInstructions;
            this.bAutoCreateResponse    = bAutoCreateResponse;
            this.bEventAsync            = bEventAsync;
        }

        public void Update()
        {
            while (bEventAsync && _mainThreadActions.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }

        public void Dispose()
        {
            if (bDisposed) return;
            bDisposed = true;

            try
            {
                // 最後一道防線：盡量避免在 UI/主緒卡住
                CloseAsync("Close Dispose").ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch { /* 最終防呆，避免例外往外拋 */ }
        }

        private async Task CloseAsync(string reason, bool bForce = false)
        {
            if (bDisposed) return;
            bConnected = false;

            // 不等待接收結束 直接強制關閉
            if (!bForce)
            {
                // 1) 先發取消，讓所有 await 能中斷
                try { _cts?.Cancel(); } catch { }

                // 2) 等 ReceiveLoop 結束（避免 race）
                try
                {
                    if (recvTask != null)
                        await recvTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { /* 正常 */ }
                catch (Exception ex) { EmitOnMain(() => OnLog($"Recv loop ended with: {ex.Message}")); }
            }

            // 3) 禮貌關閉（帶 timeout 避免無限卡住）
            try
            {
                if (_ws != null && _ws.State == WebSocketState.Open)
                {
                    using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, closeCts.Token).ConfigureAwait(false);
                }
            }
            catch { }
            finally
            {
                _ws.Dispose();
                _ws = null;

                _cts?.Dispose();
                _cts = null;
            }
        }

        // ===============================
        // Connect & session
        // ===============================
        // 如果你這是在 ASP.NET Core WebSocket gateway 裡面調用，可以直接用：
        // await client.ConnectAndConfigure(ctx.RequestAborted);
        public async Task<bool> ConnectAndConfigure(CancellationToken ct = default)
        {
            _uri    = new Uri($"wss://api.openai.com/v1/realtime?model={model}");
            _ws     = new ClientWebSocket();
            _ws.Options.SetRequestHeader("Authorization", $"Bearer {openAIApiKey}");
            _ws.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                await _ws.ConnectAsync(_uri, _cts.Token);
                bConnected = _ws.State == WebSocketState.Open;
            }
            catch (Exception ex)
            {
                EmitOnMain(() => OnError?.Invoke(AIErrorType.ConnectionFailed, $"Realtime connect failed: {ex.Message}"));
                bConnected = false;
            }

            if (bConnected)
            {
                // Minimal and valid session params
                await SendSessionUpdate();

                recvTask = ReceiveLoop();
            }

            return bConnected;
        }

        public async Task SendSessionUpdate()
        {
            var turnDetection = bAutoCreateResponse ? new
            {
                type                = "server_vad",
                threshold           = 0.5,
                prefix_padding_ms   = 300,
                silence_duration_ms = 500
            } : null;

            await SendAsync(new
            {
                type    = "session.update",
                session = new
                {
                    input_audio_format          = "pcm16",
                    output_audio_format         = "pcm16",
                    turn_detection              = turnDetection,
                    input_audio_transcription   = new { model = "gpt-4o-mini-transcribe" },
                    instructions                = basicInstructions,
                    voice
                }
            });
        }

        public Task SendAudioBase64Async(string audioBase64)
        {
            if (string.IsNullOrEmpty(audioBase64)) return Task.CompletedTask;
            return SendAsync(new { type = "input_audio_buffer.append", audio = audioBase64 });
        }

        private Task InterruptAsync()
        {
            // 取消正在產生中（語音/文字）的回覆
            return SendAsync(new { type = "response.cancel" }); // 無參數即可
        }

        // audioEndMs：本地端實際「已播放」到的毫秒數（用播放過的 sample 數換算）
        public Task TruncateAsync(int audioEndMs, int? contentIndex = null)
        {
            if (string.IsNullOrEmpty(_activeAssistantItemId)) return Task.CompletedTask;

            int idx = contentIndex ?? _activeAudioContentIndex ?? 0; // 最後才退回 0

            return SendAsync(new
            {
                type            = "conversation.item.truncate",
                item_id         = _activeAssistantItemId,
                content_index   = idx,
                audio_end_ms    = Math.Max(0, audioEndMs)
            });
        }

        /************************************
         * 中斷當前response
         * *********************************/
        public async Task BargeInAsync(float playedSeconds)
        {
            // 把秒轉毫秒
            int playedMsSoFar = (int)Math.Round(playedSeconds * 1000f);

            // 1) 停止助理的語音生成
            await InterruptAsync().ConfigureAwait(false);

            // 2) 告訴伺服器「我只聽到這裡」
            await TruncateAsync(playedMsSoFar).ConfigureAwait(false);

            // 3) 本地丟棄舊 item 的後續 delta
            _squelchItemId = _activeAssistantItemId;

            // 4) 清空本地播放緩衝，避免播放殘留
            //EmitOnMain(() => OnAssistantAudioDelta?.Invoke(Array.Empty<byte>()));

            EmitOnMain(() => OnLog($"Barge-in triggered at {playedMsSoFar} ms"));
        }

        public async Task SendTextAsync(string text, bool wantAudio = true)
        {
            if (!bConnected || string.IsNullOrWhiteSpace(text)) return;

            // 1) 先把使用者訊息加進「當前會話」
            await SendAsync(new
            {
                type    = "conversation.item.create",
                item    = new
                {
                    type    = "message",
                    role    = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text
                        }
                    }
                }
            });

            // 2) 要求模型產生回覆（可選：只文字，或同時文字+語音）
            await RequestReply(basicInstructions, wantAudio);
        }

        public async Task SendAsync(object payload)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
            {
                return;
            }

            string json = JsonConvert.SerializeObject(payload);
            var bytes   = Encoding.UTF8.GetBytes(json);

            await _sendLock.WaitAsync(_cts.Token).ConfigureAwait(false);

            try
            {
                if (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { /* ignore on shutdown */ }
            catch (WebSocketException wse) { EmitOnMain(() => OnError(AIErrorType.SendFailed, $"Send error: {wse.Message}")); }
            finally
            {
                _sendLock.Release();
            }
        }

        // ===============================
        // Receive & events
        // ===============================
        private async Task ReceiveLoop()
        {
            var userSpeechBuilder   = new StringBuilder();
            var aiSpeechBuilder     = new StringBuilder();

            while (bConnected && _ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                WebSocketReceiveResult res;
                var sb = new StringBuilder();
                try
                {
                    do
                    {
                        res = await ReceiveOnceWithTimeout(receiveIdleTimeout);
                        if (res.MessageType == WebSocketMessageType.Close)
                        {
                            EmitOnMain(() => OnLog($"Realtime closed: {res.CloseStatus} {res.CloseStatusDescription}"));
                            bConnected = false;
                            break;
                        }
                        sb.Append(Encoding.UTF8.GetString(_recvBuffer, 0, res.Count));
                    }
                    while (!res.EndOfMessage);
                }
                catch (TimeoutException)
                {
                    EmitOnMain(() => OnError?.Invoke(AIErrorType.IdleTimeout, "No frames received within timeout; close as zombie."));
                    await CloseAsync("idle-timeout", true).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    EmitOnMain(() => OnLog($"Receive error: {ex.Message}"));
                    break;
                }

                if (!bConnected)
                {
                    break;
                }

                var payload = sb.ToString();
                var lines   = payload.Split('\n');

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains("\"type\""))
                    {
                        continue;
                    }

                    HandleServerEvent(line, userSpeechBuilder, aiSpeechBuilder);
                }
            }
        }

        private void HandleServerEvent(string jsonLine, StringBuilder userSpeechBuilder, StringBuilder aiSpeechBuilder)
        {
            JObject jo;
            try { jo = JObject.Parse(jsonLine); }
            catch
            {
                if (jsonLine.Contains("\"error\"")) EmitOnMain(() => OnError(AIErrorType.InvalidPayload, $"Invalid JSON payload: {jsonLine}"));
                return;
            }

            string type = (string)jo["type"] ?? string.Empty;
            if (string.IsNullOrEmpty(type))
            {
                return;
            }

            switch (type)
            {
                // --- Response lifecycle ---
                case "response.created":
                    bResponseInFlight = true;

                    EmitOnMain(() => OnResposeStart?.Invoke());
                    return;
                case "response.completed":
                case "response.done":
                    bResponseInFlight = false;

                    EmitOnMain(() => OnResposeFinish?.Invoke());
                    return;
                case "response.output_item.added":
                    {
                        _activeAssistantItemId      = (string)jo["item"]?["id"];
                        _activeAudioContentIndex    = null;                     // 直到看到音訊 delta 才知道 index
                        _squelchItemId              = null;                     // 新 item 開始，取消消音
                        return;
                    }
                // --- Text stream ---
                case "response.audio_transcript.delta":
                case "response.output_text.delta":
                case "response.text.delta":
                    {
                        string d = (string)jo["delta"];
                        if (!string.IsNullOrEmpty(d))
                        {
                            aiSpeechBuilder.Append(d);
                        }

                        string txt = aiSpeechBuilder.ToString();
                        if (!string.IsNullOrEmpty(txt))
                        {
                            EmitOnMain(() => OnAssistantTextDelta?.Invoke(txt));
                        }
                        EmitOnMain(() => OnLog($"ASSISTANT TEXT DELTA: {txt}"));

                        return;
                    }
                case "response.audio_transcript.done":
                case "response.output_text.done":
                case "response.text.done":
                    {
                        string txt = (string)jo["transcript"];
                        //string txt = aiSpeechBuilder.ToString();
                        if (!string.IsNullOrEmpty(txt))
                        {
                            EmitOnMain(() => OnAssistantTextDone?.Invoke(txt));
                        }
                        EmitOnMain(() => OnLog($"ASSISTANT TEXT: {txt}"));
                        aiSpeechBuilder.Clear();
                        return;
                    }

                // --- Audio stream ---
                case "response.output_audio.delta":
                case "response.audio.delta":
                    {
                        // 先丟棄被消音的舊 item
                        var itemId = (string)jo["item_id"];
                        if (!string.IsNullOrEmpty(_squelchItemId) && _squelchItemId == itemId) return;

                        // 紀錄 audio 的 content_index
                        if (jo["content_index"] != null)
                        {
                            _activeAudioContentIndex = (int?)jo["content_index"];
                        }

                        // 讀取音頻內容
                        string b64 = (string)jo["delta"];
                        if (string.IsNullOrEmpty(b64))
                        {
                            return;
                        }

                        try
                        {
                            var bytes = Convert.FromBase64String(b64);

                            EmitOnMain(() => OnAssistantAudioDelta?.Invoke(bytes));
                        }
                        catch (Exception e) { EmitOnMain(() => OnError(AIErrorType.AudioDecodeFailed, $"Audio delta decode error: {e.Message}")); }
                        return;
                    }
                case "response.output_audio.done":
                    {
                        EmitOnMain(() => OnAssistantAudioDone?.Invoke());
                        return;
                    }

                // --- Assistant ASR of user audio (optional hooks) ---
                case "conversation.item.input_audio_transcription.delta":
                    {
                        string d = (string)jo["delta"];
                        if (!string.IsNullOrEmpty(d))
                        {
                            userSpeechBuilder.Append(d);
                        }

                        string txt = userSpeechBuilder.ToString();
                        if (!string.IsNullOrEmpty(txt))
                        {
                            EmitOnMain(() => OnUserTranscriptDelta?.Invoke(txt));
                        }
                        EmitOnMain(() => OnLog($"ASSISTANT TEXT DELTA: {txt}"));

                        return;
                    }
                case "conversation.item.input_audio_transcription.completed":
                    {
                        //string text = userSpeechBuilder.ToString();
                        string text = (string)jo["transcript"];
                        if (!string.IsNullOrEmpty(text))
                        {
                            EmitOnMain(() => OnUserTranscriptDone?.Invoke(text));
                            Console.WriteLine($"[Log] USER TRANSCRIPT: {text}");
                        }
                        userSpeechBuilder.Clear();

                        if (bAutoCreateResponse)
                        {
                            _ = CommitAndRequestResponseAsync(basicInstructions);
                        }
                        return;
                    }

                // --- Errors ---
                case "error":
                    {
                        string code = (string)jo["error"]?["code"];
                        string msg  = (string)jo["error"]?["message"];
                        EmitOnMain(() => OnError(AIErrorType.ServerError, $"OpenAI error: code={code}, message={msg}\n{jsonLine}"));
                        return;
                    }
                case "input_audio_buffer.committed":
                    EmitOnMain(() => OnLog("AUDIO BUFFER COMMITTED"));
                    return;

                case "conversation.item.created":
                    EmitOnMain(() => OnLog($"ITEM CREATED: role={(string)jo["item"]?["role"]}, id={(string)jo["item"]?["id"]}"));
                    return;

                case "conversation.item.input_audio_transcription.failed":
                    EmitOnMain(() => OnError?.Invoke(
                        AIErrorType.ServerError,
                        $"ASR FAILED: code={(string)jo["error"]?["code"]}, msg={(string)jo["error"]?["message"]}"
                    ));
                    return;
                default:
                    // Unhandled events are fine for now
                    return;
            }
        }

        private void OnLog(string s)
        {
            if(!bSendDebugInfo)
            {
                return;
            }

            OnLoggingDone?.Invoke(s);
        }

        private void EmitOnMain(Action action)
        {
            if (action == null) return;

            if (bEventAsync)
            {
                _mainThreadActions.Enqueue(action);
            }
            else
            {
                action.Invoke();
            }
        }

        public bool IsConnected()
        {
            return _ws != null && _ws.State == WebSocketState.Open && bConnected;
        }
        private async Task<WebSocketReceiveResult> ReceiveOnceWithTimeout(TimeSpan timeout)
        {
            using var perReceiveCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

            var recvTask    = _ws.ReceiveAsync(new ArraySegment<byte>(_recvBuffer), perReceiveCts.Token);
            var delayTask   = Task.Delay(timeout, _cts.Token);
            var finished    = await Task.WhenAny(recvTask, delayTask).ConfigureAwait(false);

            if (finished != recvTask)
            {
                // 逾時只取消「這次接收」，不影響整體連線
                perReceiveCts.Cancel();
                throw new TimeoutException("Receive idle-timeout.");
            }

            return await recvTask.ConfigureAwait(false);
        }

        /*============================================
         * 要求AI回覆訊息
         * ==========================================*/
        public async Task CommitAndRequestResponseAsync(string instructions, bool wantAudio = true)
        {
            if (!bConnected || bResponseInFlight) return;
            if (_commitInFlight) return;

            _commitInFlight = true;

            try
            {
                // 1) 使用者這一句話結束
                await SendAsync(new { type = "input_audio_buffer.commit" });

                // 2) 可以回覆了
                await RequestReply(instructions, wantAudio);
            }
            finally
            {
                _commitInFlight = false;
            }
        }

        public Task RequestReply(string instructions, bool wantAudio = true)
        {
            return SendAsync(new
            {
                type        = "response.create",
                response    = new
                {
                    modalities      = wantAudio ? new[] { "text", "audio" } : new[] { "text" },
                    instructions    = string.IsNullOrWhiteSpace(instructions) ? basicInstructions : instructions,
                }
            });
        }
    }
}
