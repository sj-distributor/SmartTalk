using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.OpenAi;

[Table("open_ai_api_key_usage_status")]
public class OpenAiApiKeyUsageStatus : IEntity, IHasModifiedFields
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("index")]
    public int Index { get; set; }
    
    [Column("using_number")]
    public int UsingNumber { get; set; }
    
    [Column("last_modified_by")]
    public int? LastModifiedBy { get; set; }
    
    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
}