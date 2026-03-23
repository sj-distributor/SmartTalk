using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Security;

namespace SmartTalk.Core.Domain.Security;

[Table("permission_rating_level")]
public class PermissionRatingLevel : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("permission_id")]
    public int PermissionId { get; set; }
    
    [Column("permission_level")]
    public PermissionLevel PermissionLevel { get; set; }
}