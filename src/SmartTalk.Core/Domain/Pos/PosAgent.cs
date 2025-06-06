using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Pos;

[Table("pos_agent")]
public class PosAgent : IEntity, IHasCreatedFields
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

    [Column("agent_id")]
    public int AgentId { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}