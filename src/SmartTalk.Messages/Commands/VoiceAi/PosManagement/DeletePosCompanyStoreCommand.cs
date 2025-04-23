using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class DeletePosCompanyStoreCommand : ICommand
{
    public int StoreId { get; set; }
}

public class DeletePosCompanyStoreResponse : SmartTalkResponse<List<PosCompanyStoreDto>>;