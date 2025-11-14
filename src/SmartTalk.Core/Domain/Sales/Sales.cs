using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Sales;

namespace SmartTalk.Core.Domain.Sales;

[Table("sales")]
public class Sales : IEntity, IHasCreatedFields, IAgent
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("name"), StringLength(255)]
    public string Name { get; set; }

    [Column("type")]
    public SalesCallType Type { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}