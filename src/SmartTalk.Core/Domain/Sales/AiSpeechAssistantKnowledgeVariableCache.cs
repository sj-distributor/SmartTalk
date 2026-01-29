using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.DynamicInterface;

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
    
    [Column("system_name")]
    public string SystemName { get; set; }

    [Column("category_name")]
    public string CategoryName { get; set; }

    [Column("field_name")]
    public string FieldName { get; set; }

    [Column("level_type")]
    public VariableLevelType LevelType { get; set; } = VariableLevelType.System;

    [Column("parent_id")]
    public int? ParentId { get; set; }

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = false;
}