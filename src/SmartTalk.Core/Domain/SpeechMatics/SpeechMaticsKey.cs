using System.ComponentModel.DataAnnotations;
using SmartTalk.Messages.Enums.SpeechMatics;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.SpeechMatics;

[Table("speech_matics_key")]
public class SpeechMaticsKey : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("key")]
    public string Key { get; set; }

    [Column("status")]
    public SpeechMaticsKeyStatus Status { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}