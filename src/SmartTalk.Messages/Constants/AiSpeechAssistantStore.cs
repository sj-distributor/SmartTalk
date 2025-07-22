using Smarties.Messages.Extensions;
using SmartTalk.Messages.Enums.OpenAi;

namespace SmartTalk.Messages.Constants;

public static class AiSpeechAssistantStore
{
    public static string DefaultUrl = "wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2025-06-03";
    public static string AiKidDefaultUrl = "wss://api.openai.com/v1/realtime?model=gpt-4o-mini-realtime-preview-2024-12-17";
    public static string GoogleDefaultUrl = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";
}