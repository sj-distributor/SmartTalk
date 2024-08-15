using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Security;

[Table("user_permission")]
public class UserPermission : IEntity
{
    public UserPermission()
    {
        CreatedDate = DateTimeOffset.Now;
        LastModifiedDate = DateTimeOffset.Now;
    }

    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
        
    [Column("last_modified_date")]
    public DateTimeOffset LastModifiedDate { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("permission_id")]
    public int PermissionId { get; set; }
}