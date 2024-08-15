using SmartTalk.Messages.Enums.System;

namespace SmartTalk.Messages.Dto.System;

public class VerifyCodeResult
{
    public bool IsValid => FailedReason == VerifyCodeFailedReason.None;
    
    public VerifyCodeFailedReason FailedReason { get; set; }
    
    public VerificationCodeDto VerificationCode { get; set; }
}