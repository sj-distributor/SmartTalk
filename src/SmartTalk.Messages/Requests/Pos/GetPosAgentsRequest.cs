using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosAgentsRequest : IRequest
{
}

public class GetPosAgentsResponse : SmartTalkResponse<List<PosAgentDto>>;