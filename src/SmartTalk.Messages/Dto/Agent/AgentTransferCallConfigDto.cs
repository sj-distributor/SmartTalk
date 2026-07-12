using System.ComponentModel.DataAnnotations;
using SmartTalk.Messages.Enums.Agent;

namespace SmartTalk.Messages.Dto.Agent;

public class AgentTransferCallConfigDto
{
    public string TransferCallNumber { get; set; }

    [Required]
    public string ServiceHours { get; set; }

    public AgentTransferCallPriority Priority { get; set; }
}
