using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_kid")]
public class AiKid : IEntity, IHasCreatedFields
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("agent_id")]
    public int AgentId { get; set; }

    [Column("kid_uuid", TypeName = "char(36)")]
    public Guid KidUuid { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}
