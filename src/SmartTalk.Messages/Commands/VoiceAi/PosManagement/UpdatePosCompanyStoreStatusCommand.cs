using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class UpdatePosCompanyStoreStatusCommand : ICommand
{
    public int StoreId { get; set; }
    
    public bool Status { get; set; }
}

public class UpdatePosCompanyStoreStatusResponse : SmartTalkResponse<PosCompanyStoreDto>;