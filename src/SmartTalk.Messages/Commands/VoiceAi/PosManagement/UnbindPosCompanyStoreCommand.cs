using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class UnbindPosCompanyStoreCommand : ICommand
{
    public int StoreId { get; set; }
}

public class UnbindPosCompanyStoreResponse : SmartiesResponse<PosCompanyStoreDto>
{
}
