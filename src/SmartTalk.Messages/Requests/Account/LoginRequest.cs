using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.System;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Account;

public class LoginRequest : IRequest
{
    public string UserName { get; set; }
    
    public string Password { get; set; }
    
    public UserAccountVerificationType VerificationType { get; set; }
}

public class LoginResponse : SmartTalkResponse<string>
{
    public VerifyCodeResult VerifyCodeResult { get; set; }
}