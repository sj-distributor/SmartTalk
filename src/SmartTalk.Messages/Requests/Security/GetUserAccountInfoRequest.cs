using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Security;

[SmartTalkAuthorize("CanCopyAccount")]
public class GetUserAccountInfoRequest : IRequest
{
    public int UserId { get; set; }
}

public class GetUserAccountInfoResponse : SmartTalkResponse<GetUserAccountInfoDto>
{
}

public class GetUserAccountInfoDto
{
    public int UserId { get; set; }
    
    public string UserName { get; set; }
    
    public string PassWord { get; set; }
}