using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetStoreUsersRequest : IRequest
{
    public int StoreId { get; set; }
}

public class GetStoreUsersResponse : SmartTalkResponse<List<StoreUserDto>>
{
}