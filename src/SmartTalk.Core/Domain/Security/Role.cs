using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Security;

namespace SmartTalk.Core.Domain.Security;

[Table("role")]
public class Role : IEntity
{
    public Role()
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
    
    [Column("name")]
    [StringLength(512)]
    public string Name { get; set; }
    
    [Column("display_name"), StringLength(255)]
    public string DisplayName { get; set; }
    
    [Column("system_source")]
    public RoleSystemSource SystemSource { get; set; }
    
    [Column("description"), StringLength(512)]
    public string Description { get; set; }
    
    [Column("is_system")]
    public bool IsSystem { get; set; }
}