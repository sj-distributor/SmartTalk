using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class UpdatePosCompanyStoreStatusCommand : ICommand
{
    public int StoreId { get; set; }
    
    public bool Status { get; set; }
}

public class UpdatePosCompanyStoreStatusResponse : SmartiesResponse<PosCompanyStoreDto>;