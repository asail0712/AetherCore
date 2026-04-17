using AetherCore.Module.AI;
using AetherCore.Utility.Attributes;

namespace AetherCore.Settings
{
    [AppSettings]
    public class OpenAISettings
    {
        public string ApiKey { get; set; }              = "";
        public string Model { get; set; }               = "gpt-4o-mini-realtime-preview";
        public string Voice { get; set; }               = "alloy";
        public string BasicInstructions { get; set; }   = "You are a helpful, concise voice assistant.";
        public bool AutoCreate { get; set; }            = false;

        public string NanoModel { get; set; }           = "gpt-5-nano";
        public string SttModel { get; set; }            = "gpt-4o-mini-transcribe";
        public string TtsModel { get; set; }            = "gpt-4o-mini-tts";

        // 新增這個
        public AISpeechProvider SpeechProvider { get; set; } = AISpeechProvider.Realtime;

        // 可選
        public int MaxHistoryMessages { get; set; } = 20;
    }
}
