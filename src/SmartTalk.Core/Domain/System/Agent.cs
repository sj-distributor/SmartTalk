using SmartTalk.Messages.Dto.Agent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.System;

[Table("agent")]
public class Agent : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("relate_id")]
    public int RelateId { get; set; }

    [Column("type")]
    public AgentType Type { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}