using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosCurrentUserStoresRequest : IRequest
{
}

public class GetPosCurrentUserStoresResponse : SmartTalkResponse<List<GetPosCurrentUserStoresResponseData>>;


public class GetPosCurrentUserStoresResponseData
{
    public PosCompanyStoreDto Store { get; set; }
    
    public List<int> AgentIds { get; set; }
}