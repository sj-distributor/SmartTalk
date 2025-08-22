using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class DeletePosCompanyStoreCommand : ICommand
{
    public int StoreId { get; set; }
}

public class DeletePosCompanyStoreResponse : SmartTalkResponse<List<PosCompanyStoreDto>>;