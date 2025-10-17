using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetStoreAgentsRequest : IRequest
{
    public List<int> AgentIds { get; set; }
}

public class GetStoreAgentsResponse : SmartTalkResponse<List<GetStoreAgentsResponseDataDto>>
{
}

public class GetStoreAgentsResponseDataDto
{
    public CompanyStoreDto Store { get; set; }
}