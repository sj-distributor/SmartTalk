using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class CheckPosCompanyRequest : IRequest
{
    public int ComapnyId { get; set; }
}

public class CheckPosCompanyResponse : SmartTalkResponse<CheckPosCompanyResponseData>;

public class CheckPosCompanyResponseData
{
    public bool IsAllow { get; set; }
    
    public string Message { get; set; }
}