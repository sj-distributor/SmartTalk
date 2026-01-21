using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AutoTest;

[Table("auto_test_call_record_sync")]
public class AutoTestCallRecordSync : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("call_log_id")]
    public string CallLogId { get; set; }

    [Column("call_id")]
    public string CallId { get; set; }

    [Column("from_number")]
    public string FromNumber { get; set; }

    [Column("to_number")]
    public string ToNumber { get; set; }

    [Column("direction")]
    public string Direction { get; set; }

    [Column("extension_id")]
    public string ExtensionId { get; set; }

    [Column("start_time_utc")]
    public DateTime StartTimeUtc { get; set; }

    [Column("recording_url")]
    public string RecordingUrl { get; set; }

    [Column("source")]
    public byte Source { get; set; }

    [Column("last_updated")]
    public DateTime LastUpdated { get; set; }
}