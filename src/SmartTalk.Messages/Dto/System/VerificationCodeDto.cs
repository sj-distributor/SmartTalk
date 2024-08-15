using SmartTalk.Messages.Enums.System;

namespace SmartTalk.Messages.Dto.System;

public class VerificationCodeDto
{
    public int Id { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public DateTimeOffset ExpiredDate { get; set; }
    
    public DateTimeOffset? AuthenticatedDate { get; set; }
    
    public int? UserAccountId { get; set; }
    
    public string Identity { get; set; }
    
    public string Code { get; set; }
    
    public string Recipient { get; set; }
    
    public int FailedAttempts { get; set; }
    
    public UserAccountVerificationCodeMethod VerificationMethod { get; set; }
    
    public UserAccountVerificationCodeAuthenticationStatus AuthenticationStatus { get; set; }
}