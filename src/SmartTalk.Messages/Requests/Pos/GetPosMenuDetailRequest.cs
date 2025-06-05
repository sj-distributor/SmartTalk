using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosMenuDetailRequest : IRequest
{
    public int Id { get; set; }
}

public class GetPosMenuDetailResponse : SmartTalkResponse<PosMenuDto>
{
}