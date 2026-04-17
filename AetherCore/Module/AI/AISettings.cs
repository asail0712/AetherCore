using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherCore.Module.AI
{
    public enum AISpeechProvider
    {
        Realtime    = 0,
        NanoSpeech  = 1
    }

    public enum AIErrorType
    {
        // ===============================
        // Lifecycle / Connection
        // ===============================
        ConnectionFailed,          // 初次連線失敗
        ConnectionClosed,          // 對方正常或非正常關閉
        IdleTimeout,               // 閒置過久被判定為殭屍連線
        Disposed,                  // 被主動 Dispose / Close

        // ===============================
        // Transport / Network
        // ===============================
        SendFailed,                // SendAsync 失敗
        ReceiveFailed,             // ReceiveAsync 失敗
        WebSocketProtocolError,    // WS 協議錯誤

        // ===============================
        // Server / OpenAI
        // ===============================
        ServerError,               // OpenAI 回傳 error 事件
        InvalidPayload,            // 收到無法解析的 JSON
        UnsupportedEvent,          // 未支援的 event type

        // ===============================
        // Audio / Media
        // ===============================
        AudioDecodeFailed,         // Base64 audio 解碼失敗
        AudioStreamInterrupted,    // 語音被中斷（barge-in）

        // ===============================
        // Client Logic
        // ===============================
        InvalidState,              // 不合法的狀態轉換
        OperationCancelled,        // CancellationToken 取消
    }
}
