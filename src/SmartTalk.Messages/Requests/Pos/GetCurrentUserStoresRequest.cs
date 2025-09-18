using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetCurrentUserStoresRequest : IRequest
{
}

public class GetCurrentUserStoresResponse : SmartTalkResponse<List<GetCurrentUserStoresResponseData>>;


public class GetCurrentUserStoresResponseData
{
    public CompanyStoreDto Store { get; set; }
    
    public List<int> AgentIds { get; set; }
}