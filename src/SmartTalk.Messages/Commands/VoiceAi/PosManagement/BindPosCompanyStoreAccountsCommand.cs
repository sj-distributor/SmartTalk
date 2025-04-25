using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class BindPosCompanyStoreAccountsCommand : ICommand
{
    public List<int> UserIds { get; set; }
    
    public int StoreId { get; set; }
}

public class BindPosCompanyStoreAccountsResponse : SmartiesResponse<List<PosStoreUserDto>>
{
}