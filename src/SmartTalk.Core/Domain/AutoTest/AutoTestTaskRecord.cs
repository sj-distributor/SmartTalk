using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Domain.AutoTest;

[Table("auto_test_task_record")]
public class AutoTestTaskRecord : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("test_task_id")]
    public int TestTaskId { get; set; }
    
    [Column("scenario_id")]
    public int ScenarioId { get; set; }
    
    [Column("data_set_id")]
    public int DataSetId { get; set; }
    
    [Column("data_set_item_id")]
    public int DataSetItemId { get; set; }
    
    [Column("input_snapshot")]
    public string InputSnapshot { get; set; }
    
    [Column("request_json")]
    public string RequestJson { get; set; }
    
    [Column("raw_output")]
    public string RawOutput { get; set; }
    
    [Column("normalized_output")]
    public string NormalizedOutput { get; set; }    
    
    [Column("evaluation_summary")]
    public string EvaluationSummary { get; set; }
    
    [Column("validation_errors")]
    public string ValidationErrors { get; set; }

    [Column("status")]
    public AutoTestTaskRecordStatus Status { get; set; } = AutoTestTaskRecordStatus.Pending;
        
    [Column("error_text")]
    public string ErrorText { get; set; }
    
    [Column("speech_matics_job_id")]
    public string SpeechMaticsJobId { get; set; }
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    
    [Column("is_archived")]
    public bool IsArchived { get; set; }
}