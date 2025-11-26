using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Sales;

[Table("ai_speech_assistant_knowledge_variable_cache")]
public class AiSpeechAssistantKnowledgeVariableCache: IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("cache_key")]
    public string CacheKey { get; set; }

    [Column("cache_value")]
    public string CacheValue { get; set; }
    
    [Column("filter")]
    public string Filter { get; set; }

    [Column("last_updated")]
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.Now;
}