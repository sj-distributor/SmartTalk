using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class ManagePosCompanyStoreAccountsCommand : ICommand
{
    public List<int> UserIds { get; set; }
    
    public int StoreId { get; set; }
}

public class ManagePosCompanyStoreAccountsResponse : SmartTalkResponse<List<PosStoreUserDto>>
{
}