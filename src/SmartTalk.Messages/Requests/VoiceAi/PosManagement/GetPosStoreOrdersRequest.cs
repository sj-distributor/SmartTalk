using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.VoiceAi.PosManagement;

public class GetPosStoreOrdersRequest : IRequest
{
    public int StoreId { get; set; }
    
    public string Keyword { get; set; }
    
    public DateTimeOffset? StartDate { get; set; }
    
    public DateTimeOffset? EndDate { get; set; }
}

public class GetPosStoreOrdersResponse : SmartTalkResponse<List<PosOrderDto>>;