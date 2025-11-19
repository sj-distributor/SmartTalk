using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetStructuredStoresRequest : IRequest
{
}

public class GetStructuredStoresResponse : SmartTalkResponse<StoreAgentsDto>
{
}