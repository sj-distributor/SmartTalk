using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdatePosCompanyStoreStatusCommand : ICommand
{
    public int StoreId { get; set; }
    
    public bool Status { get; set; }
}

public class UpdatePosCompanyStoreStatusResponse : SmartTalkResponse<PosCompanyStoreDto>;