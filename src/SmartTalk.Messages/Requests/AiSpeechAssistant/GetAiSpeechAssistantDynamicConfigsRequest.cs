using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantDynamicConfigsRequest : IRequest
{
}

public class GetAiSpeechAssistantDynamicConfigsResponse : SmartTalkResponse<GetAiSpeechAssistantDynamicConfigsResponseData>
{
}

public class GetAiSpeechAssistantDynamicConfigsResponseData
{
    public List<AiSpeechAssistantDynamicConfigDto> Configs { get; set; } = [];
}
