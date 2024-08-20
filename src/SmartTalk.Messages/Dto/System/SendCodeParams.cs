using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.System;

namespace SmartTalk.Messages.Dto.System;

public class SendCodeParams
{
    public int? UserAccountId { get; set; }
    
    public string Identity { get; set; }
    
    public string Recipient { get; set; }
    
    public int ExpirationInSeconds { get; set; } = 300;
    
    public UserAccountVerificationCodeMethod VerificationMethod { get; set; }

    public SendCodeFrom SendCodeFrom { get; set; } = SendCodeFrom.System;
}