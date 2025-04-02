using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Linphone;

[Table("linphone_sip")]
public class LinphoneSip : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("agent_id")]
    public int AgentId { get; set; }

    [Column("sip")]
    public string Sip { get; set; }

    [Column("related_agent_ids")]
    public string RelatedAgentIds { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}