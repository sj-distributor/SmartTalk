using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UnbindPosCompanyStoreCommand : ICommand
{
    public int StoreId { get; set; }
}

public class UnbindPosCompanyStoreResponse : SmartTalkResponse<PosCompanyStoreDto>
{
}
