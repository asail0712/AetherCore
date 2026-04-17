using AetherCore.Module.Interface;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;

namespace AetherCore.Module.AI
{
    internal sealed class ChatTurn
    {
        public string Role { get; set; } = "";   // system / user / assistant
        public string Text { get; set; } = "";
    }

    /// <summary>
    /// 用 STT + GPT-5 nano + TTS 模擬原本 OpenAIRealtime 的 public surface，
    /// 讓 Hub 幾乎不用改。
    /// </summary>
    public class OpenAINanoSpeechRealtime : IAISpeech, IDisposable
    {
        // ===============================
        // Config
        // ===============================
        public string openAIApiKey      = string.Empty;
        public string model             = "gpt-5-nano";                 // LLM
        public string sttModel          = "gpt-4o-mini-transcribe";     // STT
        public string ttsModel          = "gpt-4o-mini-tts";            // TTS
        public string voice             = "alloy";
        public string basicInstructions = "You are a helpful, concise voice assistant.";
        public bool bAutoCreateResponse = false;
        public bool bSendDebugInfo      = false;
        public string SetInstructions { get => basicInstructions; set => basicInstructions = value; }

        private readonly HttpClient _http;
        private readonly SemaphoreSlim _flowLock    = new(1, 1);
        private readonly SemaphoreSlim _bufferLock  = new(1, 1);

        private CancellationTokenSource? _sessionCts;
        private CancellationTokenSource? _responseCts;

        private volatile bool bConnected;
        private volatile bool bDisposed;
        private volatile bool bResponseInFlight;
        private volatile bool _commitInFlight;

        // 累積這一輪使用者的 PCM16 音訊
        private MemoryStream _inputAudioBuffer = new();

        // 本地對話歷史，取代 Realtime server-side conversation state
        private readonly List<ChatTurn> _history = new();
        private readonly int _maxHistoryMessages;

        // events
        public event Action? OnResposeStart;
        public event Action? OnResposeFinish;
        public event Action<string>? OnUserTranscriptDelta;
        public event Action<string>? OnUserTranscriptDone;
        public event Action<string>? OnAssistantTextDelta;
        public event Action<string>? OnAssistantTextDone;
        public event Action<byte[]>? OnAssistantAudioDelta;
        public event Action? OnAssistantAudioDone;
        public event Action<AIErrorType, string>? OnError;
        public event Action<string>? OnLoggingDone;

        public OpenAINanoSpeechRealtime(
            string openAIApiKey,
            string model,
            string voice,
            string basicInstructions,
            bool bAutoCreateResponse,
            string sttModel = "gpt-4o-mini-transcribe",
            string ttsModel = "gpt-4o-mini-tts",
            int maxHistoryMessages = 20)
        {
            this.openAIApiKey                           = openAIApiKey;
            this.model                                  = model;
            this.voice                                  = voice;
            this.basicInstructions                      = basicInstructions;
            this.bAutoCreateResponse                    = bAutoCreateResponse;
            this.sttModel                               = sttModel;
            this.ttsModel                               = ttsModel;
            _maxHistoryMessages                         = Math.Max(4, maxHistoryMessages);

            _http                                       = new HttpClient
            {
                BaseAddress                             = new Uri("https://api.openai.com/")
            };
            _http.DefaultRequestHeaders.Authorization   = new AuthenticationHeaderValue("Bearer", openAIApiKey);
        }

        public void Dispose()
        {
            if (bDisposed) return;
            bDisposed   = true;
            bConnected  = false;

            try { _responseCts?.Cancel(); } catch { }
            try { _sessionCts?.Cancel(); } catch { }

            _responseCts?.Dispose();
            _sessionCts?.Dispose();
            _http.Dispose();

            _inputAudioBuffer.Dispose();
            _bufferLock.Dispose();
            _flowLock.Dispose();
        }

        public Task<bool> ConnectAndConfigure(CancellationToken ct = default)
        {
            // 與 websocket 版本不同，這裡沒有真正長連線到 OpenAI。
            // 只建立 session-level cancellation token，視為 ready。
            _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bConnected  = true;
            OnLog("Nano speech pipeline ready.");
            return Task.FromResult(true);
        }

        public Task SendSessionUpdate()
        {
            // 為了相容舊 Hub，保留 no-op
            OnLog("SendSessionUpdate noop in nano+stt+tts mode.");
            return Task.CompletedTask;
        }

        public bool IsConnected()
        {
            return !bDisposed && bConnected && _sessionCts is not null && !_sessionCts.IsCancellationRequested;
        }

        /// <summary>
        /// 前端持續送入 PCM16 base64；先 buffer，不立即打 API。
        /// </summary>
        public async Task SendAudioBase64Async(string audioBase64)
        {
            if (!IsConnected() || string.IsNullOrWhiteSpace(audioBase64))
                return;

            try
            {
                var bytes = Convert.FromBase64String(audioBase64);

                await _bufferLock.WaitAsync(_sessionCts!.Token).ConfigureAwait(false);
                try
                {
                    await _inputAudioBuffer.WriteAsync(bytes, 0, bytes.Length, _sessionCts.Token).ConfigureAwait(false);
                }
                finally
                {
                    _bufferLock.Release();
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(AIErrorType.AudioDecodeFailed, $"input audio decode failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 供 speak-first 或純文字回覆時使用。
        /// </summary>
        public async Task RequestReply(string instructions, bool wantAudio = true)
        {
            if (!IsConnected() || bResponseInFlight) return;

            await _flowLock.WaitAsync(_sessionCts!.Token).ConfigureAwait(false);
            try
            {
                _responseCts?.Dispose();
                _responseCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);

                bResponseInFlight = true;
                OnResposeStart?.Invoke();

                string finalText = await GenerateAssistantTextFromHistoryAsync(
                    overrideInstructions: instructions,
                    extraUserText: null,
                    ct: _responseCts.Token).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(finalText))
                {
                    AppendHistory("assistant", finalText);
                    OnAssistantTextDone?.Invoke(finalText);

                    if (wantAudio)
                    {
                        await StreamTtsPcmAsync(finalText, _responseCts.Token).ConfigureAwait(false);
                    }
                }

                OnAssistantAudioDone?.Invoke();
            }
            catch (OperationCanceledException)
            {
                OnError?.Invoke(AIErrorType.OperationCancelled, "response cancelled");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(AIErrorType.ServerError, $"RequestReply failed: {ex.Message}");
            }
            finally
            {
                bResponseInFlight = false;
                OnResposeFinish?.Invoke();
                _flowLock.Release();
            }
        }

        public async Task SendTextAsync(string text, bool wantAudio = true)
        {
            if (!IsConnected() || string.IsNullOrWhiteSpace(text) || bResponseInFlight)
                return;

            AppendHistory("user", text);
            OnUserTranscriptDelta?.Invoke(text);
            OnUserTranscriptDone?.Invoke(text);

            await RequestReply(basicInstructions, wantAudio).ConfigureAwait(false);
        }

        /// <summary>
        /// 這是原本 RequestReply 的主要替代流程：
        /// commit 音訊 -> STT -> LLM -> TTS
        /// </summary>
        public async Task CommitAndRequestResponseAsync(string instructions, bool wantAudio = true)
        {
            if (!IsConnected() || bResponseInFlight) return;
            if (_commitInFlight) return;

            _commitInFlight = true;

            await _flowLock.WaitAsync(_sessionCts!.Token).ConfigureAwait(false);
            try
            {
                _responseCts?.Dispose();
                _responseCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);

                bResponseInFlight = true;
                OnResposeStart?.Invoke();

                var audioBytes = await DrainInputAudioBufferAsync(_responseCts.Token).ConfigureAwait(false);
                if (audioBytes.Length == 0)
                {
                    // 沒有音訊就直接依現有歷史請模型回
                    string finalTextNoAudio = await GenerateAssistantTextFromHistoryAsync(
                        overrideInstructions: instructions,
                        extraUserText: null,
                        ct: _responseCts.Token).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(finalTextNoAudio))
                    {
                        AppendHistory("assistant", finalTextNoAudio);
                        OnAssistantTextDone?.Invoke(finalTextNoAudio);

                        if (wantAudio)
                        {
                            await StreamTtsPcmAsync(finalTextNoAudio, _responseCts.Token).ConfigureAwait(false);
                        }
                    }

                    OnAssistantAudioDone?.Invoke();
                    return;
                }

                // 1) STT
                string userText = await TranscribePcm16WavAsync(audioBytes, _responseCts.Token).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(userText))
                {
                    OnUserTranscriptDelta?.Invoke(userText);
                    OnUserTranscriptDone?.Invoke(userText);
                    AppendHistory("user", userText);
                }

                // 2) nano 產生文字
                string finalText = await GenerateAssistantTextFromHistoryAsync(
                    overrideInstructions: instructions,
                    extraUserText: null,
                    ct: _responseCts.Token).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(finalText))
                {
                    AppendHistory("assistant", finalText);
                    OnAssistantTextDone?.Invoke(finalText);

                    // 3) TTS
                    if (wantAudio)
                    {
                        await StreamTtsPcmAsync(finalText, _responseCts.Token).ConfigureAwait(false);
                    }
                }

                OnAssistantAudioDone?.Invoke();
            }
            catch (OperationCanceledException)
            {
                OnError?.Invoke(AIErrorType.OperationCancelled, "commit/response cancelled");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(AIErrorType.ServerError, $"CommitAndRequestResponseAsync failed: {ex.Message}");
            }
            finally
            {
                bResponseInFlight = false;
                _commitInFlight = false;
                OnResposeFinish?.Invoke();
                _flowLock.Release();
            }
        }

        /// <summary>
        /// nano 方案只能本地取消，沒有 Realtime truncate 那麼完整。
        /// </summary>
        public Task BargeInAsync(float playedSeconds)
        {
            try
            {
                _responseCts?.Cancel();
                OnError?.Invoke(AIErrorType.AudioStreamInterrupted, $"barge-in at {playedSeconds:0.###} sec");
            }
            catch { }

            return Task.CompletedTask;
        }

        // 相容保留
        public Task TruncateAsync(int audioEndMs, int? contentIndex = null) => Task.CompletedTask;

        // ===============================
        // Internal
        // ===============================

        private void AppendHistory(string role, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            _history.Add(new ChatTurn
            {
                Role = role,
                Text = text
            });

            // 保留 system 以外最近 N 則
            if (_history.Count > _maxHistoryMessages)
            {
                int removeCount = _history.Count - _maxHistoryMessages;
                _history.RemoveRange(0, removeCount);
            }
        }

        private async Task<byte[]> DrainInputAudioBufferAsync(CancellationToken ct)
        {
            await _bufferLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var bytes = _inputAudioBuffer.ToArray();
                _inputAudioBuffer.Dispose();
                _inputAudioBuffer = new MemoryStream();
                return bytes;
            }
            finally
            {
                _bufferLock.Release();
            }
        }

        /// <summary>
        /// 你的前端目前送上來的是 PCM16，STT 端點吃檔案最穩，所以這裡包成 WAV 再上傳。
        /// </summary>
        private async Task<string> TranscribePcm16WavAsync(byte[] pcm16, CancellationToken ct)
        {
            byte[] wavBytes                     = WrapPcm16ToWav(pcm16, sampleRate: 24000, channels: 1, bitsPerSample: 16);

            using var form                      = new MultipartFormDataContent();
            using var audioContent              = new ByteArrayContent(wavBytes);
            audioContent.Headers.ContentType    = MediaTypeHeaderValue.Parse("audio/wav");

            form.Add(audioContent, "file", "input.wav");
            form.Add(new StringContent(sttModel), "model");
            form.Add(new StringContent("json"), "response_format");

            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/audio/transcriptions")
            {
                Content = form
            };

            using var res   = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            string json     = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"STT failed: {(int)res.StatusCode} {json}");

            var jo = JObject.Parse(json);
            return (string?)jo["text"] ?? string.Empty;
        }

        private async Task<string> GenerateAssistantTextFromHistoryAsync(
            string? overrideInstructions,
            string? extraUserText,
            CancellationToken ct)
        {
            string systemPrompt = string.IsNullOrWhiteSpace(overrideInstructions)
                ? basicInstructions
                : overrideInstructions!;

            var input = new List<object>
            {
                new
                {
                    role    = "system",
                    content = systemPrompt
                }
            };

            foreach (var turn in _history)
            {
                input.Add(new
                {
                    role    = turn.Role,
                    content = turn.Text
                });
            }

            if (!string.IsNullOrWhiteSpace(extraUserText))
            {
                input.Add(new
                {
                    role    = "user",
                    content = extraUserText
                });
            }

            var payload = new
            {
                model,
                stream      = true,
                reasoning   = new
                {
                    effort = "minimal"
                },
                text = new
                {
                    verbosity = "low"
                },
                input
            };

            using var req   = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
            req.Content     = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            using var res   = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                string err  = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException($"Responses failed: {(int)res.StatusCode} {err}");
            }

            using var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            string? line;
            var fullText = new StringBuilder();

            while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data:"))
                    continue;

                string data = line["data:".Length..].Trim();
                if (data == "[DONE]")
                    break;

                JObject jo;
                try
                {
                    jo = JObject.Parse(data);
                }
                catch
                {
                    continue;
                }

                string eventType = (string?)jo["type"] ?? "";
                switch (eventType)
                {
                    case "response.output_text.delta":
                        {
                            string delta = (string?)jo["delta"] ?? "";
                            if (!string.IsNullOrEmpty(delta))
                            {
                                fullText.Append(delta);
                                OnAssistantTextDelta?.Invoke(fullText.ToString());
                            }
                            break;
                        }
                    case "response.output_text.done":
                        {
                            string text = (string?)jo["text"] ?? "";
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                // 有些情況 server 直接給完整 text
                                if (fullText.Length == 0)
                                {
                                    fullText.Append(text);
                                    OnAssistantTextDelta?.Invoke(fullText.ToString());
                                }
                            }
                            break;
                        }
                    case "response.completed":
                    case "response.done":
                        {
                            // 結束由外層處理
                            break;
                        }
                    case "error":
                        {
                            string msg = (string?)jo["error"]?["message"] ?? data;
                            throw new InvalidOperationException($"responses stream error: {msg}");
                        }
                }
            }

            return fullText.ToString();
        }

        private async Task StreamTtsPcmAsync(string text, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var payload = new
            {
                model = ttsModel,
                input = text,
                voice,
                response_format = "pcm"
            };

            using var req   = new HttpRequestMessage(HttpMethod.Post, "v1/audio/speech");
            req.Content     = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json");

            using var res = await _http.SendAsync(
                req,
                HttpCompletionOption.ResponseHeadersRead,
                ct).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                string err = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException($"TTS failed: {(int)res.StatusCode} {err}");
            }

            using var stream    = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            byte[] buffer       = new byte[4096];
            int read;
            byte? pendingByte   = null;

            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                int totalLength = read + (pendingByte.HasValue ? 1 : 0);
                int evenLength  = totalLength & ~1; // 取偶數長度

                if (evenLength == 0)
                {
                    pendingByte = buffer[0];
                    continue;
                }

                byte[] chunk    = new byte[evenLength];
                int offset      = 0;

                if (pendingByte.HasValue)
                {
                    chunk[0]    = pendingByte.Value;
                    offset      = 1;
                    pendingByte = null;
                }

                int copyCount = evenLength - offset;
                Buffer.BlockCopy(buffer, 0, chunk, offset, copyCount);

                // 如果這次總長度是奇數，最後一個 byte 留到下次
                if (totalLength > evenLength)
                {
                    pendingByte = buffer[read - 1];
                }

                OnAssistantAudioDelta?.Invoke(chunk);
            }

            // 理論上 PCM16 正常結束不該剩 1 byte
            if (pendingByte.HasValue)
            {
                OnLog("TTS stream ended with 1 dangling byte.");
            }
        }

        private void OnLog(string s)
        {
            if (!bSendDebugInfo) return;
            OnLoggingDone?.Invoke(s);
        }

        private static byte[] WrapPcm16ToWav(byte[] pcmData, int sampleRate, short channels, short bitsPerSample)
        {
            int byteRate        = sampleRate * channels * bitsPerSample / 8;
            short blockAlign    = (short)(channels * bitsPerSample / 8);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // RIFF header
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + pcmData.Length);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write(channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);

            // data chunk
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(pcmData.Length);
            bw.Write(pcmData);

            bw.Flush();
            return ms.ToArray();
        }
    }
}