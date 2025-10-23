using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Domain.AutoTest;

[Table("auto_test_task")]
public class AutoTestTask : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("scenario_id")]
    public int ScenarioId { get; set; }
    
    [Column("data_set_id")]
    public int DataSetId { get; set; }
    
    [Column("params")]
    public string Params { get; set; }
    
    [Column("status")]
    public AutoTestTaskStatus Status { get; set; } = AutoTestTaskStatus.Pending;
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    
    [Column("started_at")]
    public DateTimeOffset? StartedAt { get; set; }
    
    [Column("finished_at")]
    public DateTimeOffset? FinishedAt { get; set; }
}