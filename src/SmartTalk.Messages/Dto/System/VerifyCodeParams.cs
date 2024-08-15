using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.System;

namespace SmartTalk.Messages.Dto.System;

public class VerifyCodeParams
{
    public int? UserAccountId { get; set; }
    
    public string Identity { get; set; }
    
    public string Code { get; set; }
    
    public string Recipient { get; set; }

    public int MaxAttempts { get; set; } = 5;
    
    public bool IncreaseFailedAttempts => true; 
    
    public UserAccountVerificationCodeMethod? VerificationMethod { get; set; }
}