using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Domain.AutoTest;

[Table("auto_test_scenario")]
public class AutoTestScenario : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("key_name")]
    public string KeyName { get; set; }
    
    [Column("name")]
    public string Name { get; set; }
    
    [Column("input_schema")]
    public string InputSchema { get; set; }
    
    [Column("output_schema")]
    public string OutputSchema { get; set; }
    
    [Column("action_config")]
    public string ActionConfig { get; set; }
    
    [Column("action_type")]
    public AutoTestActionType ActionType { get; set; }
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}