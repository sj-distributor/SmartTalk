using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.Account;

namespace SmartTalk.Messages.Commands.Authority;

public class GetAccountsCommand : ICommand
{
    public int? PageIndex { get; set; } = 1;

    public int? PageSize { get; set; } = 10;
    
    public string UserName { get; set; }
}

public class GetAccountsResponse : SmartTalkResponse<List<UserAccountDto>>
{
}