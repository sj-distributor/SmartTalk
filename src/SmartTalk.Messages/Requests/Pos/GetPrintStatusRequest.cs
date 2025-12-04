using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPrintStatusRequest : IRequest
{
    public long OrderId { get; set; }
}

public class GetPrintStatusResponse : SmartTalkResponse<PosOrderDto>
{
}