using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.Account;

namespace SmartTalk.Messages.Requests.Authority;

public class GetUserAccountsRequest : IRequest
{
    public int? PageIndex { get; set; } = 1;

    public int? PageSize { get; set; } = 10;
    
    public string UserName { get; set; }
}

public class GetUserAccountsResponse : SmartTalkResponse
{
    public List<UserAccountDto> UserAccounts { get; set; }

    public int Count { get; set; }
}