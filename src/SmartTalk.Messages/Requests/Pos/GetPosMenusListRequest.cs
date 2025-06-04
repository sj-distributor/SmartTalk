using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosMenusListRequest : IRequest
{
    public int StoreId { get; set; }
}

public class GetPosMenusListResponse : SmartTalkResponse<List<PosMenuDto>>
{
}