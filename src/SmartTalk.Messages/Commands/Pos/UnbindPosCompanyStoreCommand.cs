using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Messages.Commands.Pos;

public class UnbindPosCompanyStoreCommand : ICommand
{
    public int StoreId { get; set; }
}

public class UnbindPosCompanyStoreResponse : SmartiesResponse<PosCompanyStoreDto>
{
}
