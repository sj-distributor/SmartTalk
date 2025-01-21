using Smarties.Messages.Extensions;
using SmartTalk.Messages.Enums.OpenAi;

namespace SmartTalk.Messages.Constants;

public static class AiSpeechAssistantStore
{
    public static string DefaultUrl = $"wss://api.openai.com/v1/realtime?model={OpenAiRealtimeModel.Gpt4o1217.GetDescription()}";
}