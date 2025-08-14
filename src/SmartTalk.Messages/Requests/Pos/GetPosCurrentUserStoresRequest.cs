using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosCurrentUserStoresRequest : IRequest
{
}

public class GetPosCurrentUserStoresResponse : SmartTalkResponse<List<PosCompanyStoreDto>>;