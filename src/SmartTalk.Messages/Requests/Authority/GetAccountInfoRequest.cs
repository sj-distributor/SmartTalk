using Mediator.Net.Contracts;
using Smarties.Messages.Attributes;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Authority;

[SmartiesAuthorize("CanCopyAccount")]
public class GetAccountInfoRequest : IRequest
{
    public int UserId { get; set; }
}

public class GetAccountInfoResponse : SmartTalkResponse
{
    public int UserId { get; set; }
    
    public string UserName { get; set; }
    
    public string PassWord { get; set; }
}