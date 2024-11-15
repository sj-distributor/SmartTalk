using Autofac;
using Mediator.Net;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.IntegrationTests.Utils.Account;

public class AccountUtil : TestUtil
{
    public AccountUtil(ILifetimeScope scope) : base(scope)
    {
    }

    public async Task<UserAccount> AddUserAccount(
        string userName, string password, bool isActive = true, UserAccountIssuer issuer = UserAccountIssuer.Self, string? thirdPartyId = null)
    {
        var account = new UserAccount
        {
            UserName = userName,
            Password = password.ToSha256(),
            IsActive = isActive,
            Issuer = issuer,
            ThirdPartyUserId = thirdPartyId
        };
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAsync(account);
        });
        
        return account;
    }
}