using AetherCore.Module.Interface;
using AetherCore.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherCore.Module.AI
{
    public static class AISpeechFactory
    {
        public static IAISpeech Create(
            OpenAISettings opt,
            string instructions,
            string voice)
        {
            return opt.SpeechProvider switch
            {
                AISpeechProvider.NanoSpeech
                => new OpenAINanoSpeechRealtime(
                    openAIApiKey: opt.ApiKey,
                    model: opt.NanoModel,
                    voice: voice,
                    basicInstructions: instructions,
                    bAutoCreateResponse: opt.AutoCreate,
                    sttModel: opt.SttModel,
                    ttsModel: opt.TtsModel,
                    maxHistoryMessages: opt.MaxHistoryMessages
                ),

                AISpeechProvider.Realtime
                => new OpenAIRealtime(
                    openAIApiKey: opt.ApiKey,
                    model: opt.Model,
                    voice: voice,
                    basicInstructions: instructions,
                    bAutoCreateResponse: opt.AutoCreate,
                    bEventAsync: false
                ),
                _
                => new OpenAIRealtime(
                    openAIApiKey: opt.ApiKey,
                    model: opt.Model,
                    voice: voice,
                    basicInstructions: instructions,
                    bAutoCreateResponse: opt.AutoCreate,
                    bEventAsync: false
                ),
            };
        }
    }
}