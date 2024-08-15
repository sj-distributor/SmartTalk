using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Account;

[Table("user_account_profile")]
public class UserAccountProfile : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("user_account_id")]
    public int UserAccountId { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;

    [Column("display_name"), StringLength(512)]
    public string DisplayName { get; set; }
    
    [Column("phone"), StringLength(50)]
    public string Phone { get; set; }
    
    [Column("email"), StringLength(128)]
    public string Email { get; set; }
}