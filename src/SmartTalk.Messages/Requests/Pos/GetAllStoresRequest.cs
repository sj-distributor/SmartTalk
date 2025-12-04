using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetAllStoresRequest : HasServiceProviderId, IRequest
{
}

public class GetAllStoresResponse: SmartTalkResponse<List<CompanyStoreDto>>
{
}