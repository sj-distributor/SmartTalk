using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Core.Domain;

namespace SmartTalk.Core.Domain.AutoTest;

[Table("auto_test_data_set")]
public class AutoTestDataSet : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("scenario_id")]
    public int ScenarioId { get; set; }
    
    [Column("key_name")]
    public string KeyName { get; set; }
    
    [Column("name")]
    public string Name { get; set; }
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}