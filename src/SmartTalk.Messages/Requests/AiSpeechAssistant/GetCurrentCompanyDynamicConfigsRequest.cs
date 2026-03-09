using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetCurrentCompanyDynamicConfigsRequest : IRequest
{
    public int StoreId { get; set; }
}

public class GetCurrentCompanyDynamicConfigsResponse : SmartTalkResponse<GetCurrentCompanyDynamicConfigsResponseData>
{
}

public class GetCurrentCompanyDynamicConfigsResponseData
{
    public CompanyStoreDto Store { get; set; }

    public List<AiSpeechAssistantDynamicConfigDto> Configs { get; set; } = [];
}
