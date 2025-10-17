using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetAgentsStoresRequest : IRequest
{
    public List<int> AgentIds { get; set; }
}

public class GetAgentsStoresResponse : SmartTalkResponse<List<GetAgentsStoresResponseDataDto>>
{
}

public class GetAgentsStoresResponseDataDto
{
    public CompanyStoreDto Store { get; set; }
}