using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Security;

[Table("permission")]
public class Permission : IEntity
{
    public Permission()
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

    [Column("name"), StringLength(255)]
    public string Name { get; set; }
    
    [Column("display_name"), StringLength(255)]
    public string DisplayName { get; set; }
    
    [Column("description"), StringLength(512)]
    public string Description { get; set; }
    
    [Column("is_system")]
    public bool IsSystem { get; set; }
}