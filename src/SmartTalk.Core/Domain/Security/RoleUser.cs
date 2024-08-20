using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Security;

[Table("role_user")]
public class RoleUser : IEntity
{
    public RoleUser()
    {
        CreatedOn = DateTimeOffset.Now;
        ModifiedOn = DateTimeOffset.Now;
        Uuid = Guid.NewGuid();
    }
    
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("created_on")]
    public DateTimeOffset CreatedOn { get; set; }
    
    [Column("modified_on")]
    public DateTimeOffset ModifiedOn { get; set; }
    
    [Column("uuid", TypeName = "varchar(36)")]
    public Guid Uuid { get; set; }
    
    [Column("role_id")]
    public int RoleId { get; set; }
    
    [Column("user_id")]
    public int UserId { get; set; }
}