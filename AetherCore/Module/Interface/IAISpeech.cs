using AetherCore.Module.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherCore.Module.Interface
{
    public interface IAISpeech
    {
        event Action OnResposeStart;
        event Action OnResposeFinish;
        event Action<string> OnUserTranscriptDelta;
        event Action<string> OnUserTranscriptDone;
        event Action<string> OnAssistantTextDelta;
        event Action<string> OnAssistantTextDone;
        event Action<byte[]> OnAssistantAudioDelta;
        event Action OnAssistantAudioDone;
        event Action<AIErrorType, string> OnError;
        event Action<string> OnLoggingDone; // 純資訊用（可選）


        // 音訊輸入
        Task SendAudioBase64Async(string audioBase64);
        // 音訊輸入結束，請AI回覆
        Task CommitAndRequestResponseAsync(string instructions, bool wantAudio = true);
        // 直接要求回覆
        Task RequestReply(string instructions, bool wantAudio = true);
        // 中斷當次回話
        Task BargeInAsync(float playedSeconds);

        // 判斷是否連線
        bool IsConnected();
        // 連線
        Task<bool> ConnectAndConfigure(CancellationToken ct = default);
        // 釋放資源
        void Dispose();
        // 更新session
        Task SendSessionUpdate();

        // 設定提示詞
        string SetInstructions { get; set; }
    }
}
