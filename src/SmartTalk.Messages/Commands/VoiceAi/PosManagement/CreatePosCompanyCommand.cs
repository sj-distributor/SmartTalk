using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class CreatePosCompanyCommand : ICommand
{
    public string Name { get; set; }

    public string Description { get; set; }
}

public class CreatePosCompanyResponse : SmartTalkResponse<PosCompanyDto>
{
}