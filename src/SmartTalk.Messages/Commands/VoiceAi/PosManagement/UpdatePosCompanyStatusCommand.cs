using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class UpdatePosCompanyStatusCommand : ICommand
{
    public int Id { get; set; }
    
    public bool Status { get; set; }
}

public class UpdatePosCompanyStatusResponse : SmartTalkResponse<PosCompanyDto>
{
}