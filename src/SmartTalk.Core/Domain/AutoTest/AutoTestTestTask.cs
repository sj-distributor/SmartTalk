using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Domain.AutoTest;

[Table("auto_test_test_task")]
public class AutoTestTestTask : IEntity
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
    public AutoTestStatus Status { get; set; }
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    
    [Column("started_at")]
    public DateTimeOffset StartedAt { get; set; }
    
    [Column("finished_at")]
    public DateTimeOffset FinishedAt { get; set; }
}