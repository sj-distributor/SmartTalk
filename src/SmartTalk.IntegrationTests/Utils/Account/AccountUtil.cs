using Autofac;
using Mediator.Net;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.IntegrationTests.Utils.Account;

public class AccountUtil : TestUtil
{
    public AccountUtil(ILifetimeScope scope) : base(scope)
    {
    }

    public async Task<UserAccount> AddUserAccount(
        string userName, string password, int? roleId = null, bool isActive = true, UserAccountIssuer issuer = UserAccountIssuer.Self, string? thirdPartyId = null, string creator = null)
    {
        var account = new UserAccount
        {
            UserName = userName,
            Password = password.ToSha256(),
            OriginalPassword = password,
            IsActive = isActive,
            Issuer = issuer,
            ThirdPartyUserId = thirdPartyId,
            Creator = creator
        };
      
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAsync(account).ConfigureAwait(false);
        });


        await RunWithUnitOfWork<IRepository>(async repository =>
        {

            if (roleId.HasValue)
            {
                var roleUser = new RoleUser
                {
                    RoleId = roleId.Value,
                    UserId = account.Id
                };

                await repository.InsertAsync(roleUser).ConfigureAwait(false);
            }

            var profile = new UserAccountProfile
            {
                UserAccountId = account.Id,
                DisplayName = userName
            };
           
            await repository.InsertAsync(profile).ConfigureAwait(false);
        });
                
        return account;
    }
}