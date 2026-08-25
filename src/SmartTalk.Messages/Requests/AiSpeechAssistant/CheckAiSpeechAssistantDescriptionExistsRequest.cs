using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class CheckAiSpeechAssistantDescriptionExistsRequest : IRequest
{
    public string ItemDescription { get; set; }
}

public class CheckAiSpeechAssistantDescriptionExistsResponse : SmartTalkResponse<CheckAiSpeechAssistantDescriptionExistsResponseData>;

public class CheckAiSpeechAssistantDescriptionExistsResponseData
{
    public bool Result { get; set; }
}
