using SmartTalk.Messages.Enums.System;

namespace SmartTalk.Messages.Dto.System;

public class SendCodeResult
{
    public bool IsSucceeded { get; set; }
    
    public string ErrMsg { get; set; }
    
    public SendCodeAction Action { get; set; }
    
    public VerificationCodeDto VerificationCode { get; set; }
}