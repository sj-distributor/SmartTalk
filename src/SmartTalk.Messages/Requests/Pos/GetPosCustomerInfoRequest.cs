using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosCustomerInfoRequest : IRequest
{
    public string Phone { get; set; }
}

public class GetPosCustomerInfoResponse : SmartTalkResponse<List<PosCustomerInfoDto>>;