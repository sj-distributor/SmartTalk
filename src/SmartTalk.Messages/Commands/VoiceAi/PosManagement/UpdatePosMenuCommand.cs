using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class UpdatePosMenuCommand : ICommand
{
    public int Id { get; set; }
    
    public string TimePeriod { get; set; }
    
    public bool Status { get; set; }
}

public class UpdatePosMenuResponse : SmartTalkResponse<PosMenuDto>
{
}