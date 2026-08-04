using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Agent;

namespace SmartTalk.Core.Domain.System;

[Table("agent_transfer_call_config")]
public class AgentTransferCallConfig : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("agent_id")]
    public int AgentId { get; set; }

    [Column("transfer_call_number"), StringLength(128)]
    public string TransferCallNumber { get; set; }

    [Required]
    [Column("service_hours")]
    public string ServiceHours { get; set; }

    [Column("priority")]
    public AgentTransferCallPriority Priority { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}
