using Autofac;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Core.Services.Identity;

namespace SmartTalk.IntegrationTests.Utils.Account;

public class IdentityUtil : TestUtil
{
    public IdentityUtil(ILifetimeScope scope) : base(scope)
    {
    }

    public async Task CreateUser(TestCurrentUser testUser)
    {
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAsync(new UserAccount
            {
                Id = testUser.Id.Value,
                UserName = testUser.UserName,
                Password = "123456",
                IsActive = true
            });
        });
    }

    public void SwitchUser(ContainerBuilder builder, TestCurrentUser signUser)
    {
        builder.RegisterInstance(signUser).As<ICurrentUser>();
    }
    
    public async Task InsertRolesAsync(params string[] roleNames)
    {
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            foreach (var roleName in roleNames)
            {
                var roles = await repository.QueryNoTracking<Role>().Where(x => x.Name == roleName).ToListAsync().ConfigureAwait(false);

                if (roles.Any()) continue; 

                await repository.InsertAsync(
                    new Role
                    {
                        Name = roleName, Uuid = Guid.NewGuid()
                    }, CancellationToken.None).ConfigureAwait(false);
            }
        });
    }
}