using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class ManagePosCompanyStoreAccountsCommand : ICommand
{
    public List<int> UserIds { get; set; }
    
    public int StoreId { get; set; }
}

public class ManagePosCompanyStoreAccountsResponse : SmartTalkResponse<List<PosStoreUserDto>>
{
}