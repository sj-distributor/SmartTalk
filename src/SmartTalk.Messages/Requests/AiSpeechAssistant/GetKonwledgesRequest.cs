using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetKonwledgesRequest : IRequest
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 10;
    
    public int CompanyId { get; set; }
    
    public int? StoreId { get; set; }
    
    public int? AgentId { get; set; }
    
    public string KeyWord { get; set; }
}

public class GetKonwledgesResponse : SmartTalkResponse<List<GetKonwledgesResponseData>>
{
}

public class GetKonwledgesResponseData
{
    public int AssistantId { get; set; }
    
    public string AssiatantName { get; set; }
    
    public string StoreName { get; set; }
    
    public int KnowledgeId { get; set; }
    
    public string AiAgentName { get; set; }
}