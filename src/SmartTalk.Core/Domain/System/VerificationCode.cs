using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.Core.Domain.System;

[Table("verification_code")]
public class VerificationCode : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
    
    [Column("expired_date")]
    public DateTimeOffset ExpiredDate { get; set; }
    
    [Column("authenticated_date")]
    public DateTimeOffset? AuthenticatedDate { get; set; }
    
    [Column("user_account_id")]
    public int? UserAccountId { get; set; }
    
    [Column("identity"), StringLength(128)]
    public string Identity { get; set; }
    
    [Column("code"), StringLength(64)]
    public string Code { get; set; }
    
    [Column("recipient"), StringLength(128)]
    public string Recipient { get; set; }
    
    [Column("failed_attempts")]
    public int FailedAttempts { get; set; }
    
    [Column("verification_method")]
    public UserAccountVerificationCodeMethod VerificationMethod { get; set; }
    
    [Column("authentication_status")]
    public UserAccountVerificationCodeAuthenticationStatus AuthenticationStatus { get; set; } = UserAccountVerificationCodeAuthenticationStatus.Pending;
}