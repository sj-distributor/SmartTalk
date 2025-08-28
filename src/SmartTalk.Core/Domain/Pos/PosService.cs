using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Pos;

public class PosService : IEntity, IHasCreatedFields, IHasModifiedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }
    
    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}