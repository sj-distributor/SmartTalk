using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class DeletePosCompanyCommand : ICommand
{
    public int Id { get; set; }
}

public class DeletePosCompanyResponse : SmartTalkResponse<PosCompanyDto>
{
}