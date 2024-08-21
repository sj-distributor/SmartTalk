using Autofac;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Account;
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
}