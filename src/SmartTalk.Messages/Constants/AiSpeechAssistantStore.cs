using Smarties.Messages.Extensions;
using SmartTalk.Messages.Enums.OpenAi;

namespace SmartTalk.Messages.Constants;

public static class AiSpeechAssistantStore
{
    public static string DefaultUrl = $"wss://api.openai.com/v1/realtime?model={OpenAiRealtimeModel.Gpt4o1217.GetDescription()}";
    public static string AiKidDefaultUrl = "wss://api.openai.com/v1/realtime?model=gpt-4o-mini-realtime-preview-2024-12-17";
}