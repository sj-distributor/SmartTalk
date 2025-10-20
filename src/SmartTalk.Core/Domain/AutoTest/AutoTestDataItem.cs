using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AutoTest;

[Table("auto_test_data_item")]
public class AutoTestDataItem : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("scenario_id")]
    public int ScenarioId { get; set; }
    
    [Column("import_record_id")]
    public int ImportRecordId { get; set; }
    
    [Column("input_json")]
    public string InputJson { get; set; }
    
    [Column("quality")]
    public string Quality { get; set; }
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}