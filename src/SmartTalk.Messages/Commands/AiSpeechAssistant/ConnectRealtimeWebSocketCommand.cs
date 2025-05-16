using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.OpenAi;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class ConnectRealtimeWebSocketCommand : ICommand
{
    public int? AssistantId { get; set; }
    
    public string CustomPrompt { get; set; }
}

public class ConnectRealtimeWebSocketResponse : SmartTalkResponse<OpenAiRealtimeSessionDto>;

