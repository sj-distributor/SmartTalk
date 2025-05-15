using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class UpdatePosCompanyCommand : ICommand
{
    public int Id { get; set; }
    
    public string Name { get; set; }

    public string Description { get; set; }
}

public class UpdatePosCompanyResponse : SmartTalkResponse<PosCompanyDto>
{
}