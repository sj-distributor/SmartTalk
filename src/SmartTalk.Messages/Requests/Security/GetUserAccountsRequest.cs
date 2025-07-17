using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.Messages.Requests.Security;

[SmartTalkAuthorize(Permissions = new[] { SecurityStore.Permissions.CanViewAccountManagement})]
public class GetUserAccountsRequest : IRequest
{
    public int? PageIndex { get; set; } = 1;

    public int? PageSize { get; set; } = 10;
    
    public string UserName { get; set; }
    
    public UserAccountLevel? UserAccountLevel { get; set; }
}

public class GetUserAccountsResponse : SmartTalkResponse<GetUserAccountsDto>
{
}

public class GetUserAccountsDto
{
    public List<UserAccountDto> UserAccounts { get; set; }

    public int Count { get; set; }
}