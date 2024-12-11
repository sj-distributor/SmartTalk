using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Security;

[Table("role_permission_user")]
public class RolePermissionUser : IEntity
{
    public RolePermissionUser()
    {
        CreatedDate = DateTimeOffset.Now;
        ModifiedDate = DateTimeOffset.Now;
    }
    
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
    
    [Column("modified_date")]
    public DateTimeOffset ModifiedDate { get; set; }

    [Column("role_id")]
    public int RoleId { get; set; }
    
    [Column("permission_id")]
    public int PermissionId { get; set; }
    
    [Column("user_ids")]
    public string UserIds { get; set; }
}