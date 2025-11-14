using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetStoresAgentsRequest : IRequest
{
    public List<int> StoreIds { get; set; }
}

public class GetStoresAgentsResponse : SmartTalkResponse<List<GetStoresAgentsResponseDataDto>>
{
}

public class GetStoresAgentsResponseDataDto
{
    public CompanyStoreDto Store { get; set; }
    
    public List<int> AgentIds { get; set; }
}