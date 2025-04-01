using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Linphone;

namespace SmartTalk.Core.Domain.Linphone;

[Table("linphone_cdr")]
public class LinphoneCdr : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("call_date")] 
    public long CallDate { get; set; }
    
    [Column("caller")]
    public string Caller { get; set; }

    [Column("targetter")]
    public string Targetter { get; set; }

    [Column("status")]
    public LinphoneStatus Status { get; set; }

    [Column("agent_id")]
    public int AgentId { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}