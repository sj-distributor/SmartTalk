using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.SpeechMatics;

[Table("check_first_sentence_prompt")]
public class CheckFirstSentencePrompt : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("agent_id")]
    public int AgentId { get; set; }

    [Column("prompt")]
    public string Prompt { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}