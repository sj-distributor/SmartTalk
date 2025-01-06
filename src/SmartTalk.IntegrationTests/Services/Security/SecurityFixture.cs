using System.Text.RegularExpressions;
using Mediator.Net;
using Shouldly;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.Security;
using SmartTalk.IntegrationTests.TestBaseClasses;
using SmartTalk.IntegrationTests.Utils.Account;
using SmartTalk.IntegrationTests.Utils.Security;
using SmartTalk.Messages.Commands.Security;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests.Security;
using Xunit;
using Xunit.Abstractions;

namespace SmartTalk.IntegrationTests.Services.Security;

public class SecurityFixture : SecurityFixtureBase
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly SecurityUtil _securityUtil;
    private readonly AccountUtil _accountUtil;
    
    public SecurityFixture(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _securityUtil = new SecurityUtil(CurrentScope);
        _accountUtil = new AccountUtil(CurrentScope);
    }
    
    [Fact]
    public async Task CanCreateUserAccountAsync()
    {
        var roleId = await _securityUtil.AddRolesAsync().ConfigureAwait(false);

        await RunWithUnitOfWork<IMediator, IRepository>(async (mediator, respository) =>
        {
            var userAccount = await mediator.SendAsync<CreateUserAccountCommand, CreateUserAccountResponse>(new CreateUserAccountCommand
            {
                UserName = "johnny",
                RoleId = roleId
            }).ConfigureAwait(false);
            
            var account = await respository.FirstOrDefaultAsync<UserAccount>(x => x.Id == userAccount.Data.Id).ConfigureAwait(false);
            var accountProfile = await respository.FirstOrDefaultAsync<UserAccountProfile>(x => x.UserAccountId == userAccount.Data.Id).ConfigureAwait(false);
            var role = await respository.FirstOrDefaultAsync<RoleUser>(x => x.RoleId == roleId && x.UserId == userAccount.Data.Id).ConfigureAwait(false);
            
            account?.Id.ShouldBe(userAccount.Data.Id);
            account?.UserName.ShouldBe("johnny");
            account?.Creator.ShouldBe("TEST_USER");
            role?.RoleId.ShouldBe(roleId);
            role?.UserId.ShouldBe(userAccount.Data.Id);
            accountProfile?.UserAccountId.ShouldBe(userAccount.Data.Id);
        });
    }

    [Fact]
    public async Task CanGetPhoneOrderRecordsAsync()
    {
        var roleId = await _securityUtil.AddRolesAsync("User").ConfigureAwait(false);
        
        await _accountUtil.AddUserAccount("johnny", "123456", roleId, creator: "TEST_USER").ConfigureAwait(false);
        await _accountUtil.AddUserAccount("travice", "1234567", roleId, creator: "TEST_USER").ConfigureAwait(false);
        await _accountUtil.AddUserAccount("open", "1234567", roleId, creator: "TEST_USER", issuer: UserAccountIssuer.Wiltechs).ConfigureAwait(false);
        
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            var records = await mediator.RequestAsync<GetUserAccountsRequest, GetUserAccountsResponse>(new GetUserAccountsRequest()).ConfigureAwait(false);
            
            records.Data.Count.ShouldBe(3);
            
            records.Data.UserAccounts[0].UserName.ShouldBe("travice");
            records.Data.UserAccounts[0].Roles[0].Name.ShouldBe("User");
            records.Data.UserAccounts[0].LastModifiedByName.ShouldBe("TEST_USER");
            
            records.Data.UserAccounts[1].UserName.ShouldBe("johnny");
            records.Data.UserAccounts[1].Roles[0].Name.ShouldBe("User");
            records.Data.UserAccounts[1].LastModifiedByName.ShouldBe("TEST_USER");
        });
    }

    [Fact]
    public async Task CanUpdateUserAccountAsync()
    {
        var userRoleId = await _securityUtil.AddRolesAsync("User").ConfigureAwait(false);
        var adminRoleId = await _securityUtil.AddRolesAsync("Administrator").ConfigureAwait(false);
        
        var account = await _accountUtil.AddUserAccount("张三", "123456", userRoleId).ConfigureAwait(false);

        await RunWithUnitOfWork<IMediator, IRepository>(async (mediator, repository) =>
        {
            await mediator.SendAsync<UpdateUserAccountCommand, UpdateUserAccountResponse>(new UpdateUserAccountCommand
            {
                UserId = account.Id,
                OldRoleId = userRoleId,
                NewRoleId = adminRoleId
            }).ConfigureAwait(false);

            var roleUser = await repository.FirstOrDefaultAsync<RoleUser>(x => x.UserId == account.Id).ConfigureAwait(false);

            roleUser?.RoleId.ShouldBe(adminRoleId);
        });
    }
    
    [Fact]
    public async Task CanDeleteUserAccountsAsync()
    {
        var adminRoleId = await _securityUtil.AddRolesAsync("Administrator").ConfigureAwait(false);
        
        var account = await _accountUtil.AddUserAccount("张三", "123456", adminRoleId).ConfigureAwait(false);
        
        await RunWithUnitOfWork<IMediator, IRepository>(async (mediator, repository) =>
        {
            await mediator.SendAsync<DeleteUserAccountsCommand, DeleteUserAccountsResponse>(new DeleteUserAccountsCommand
            {
                UserId = account.Id,
                RoleId = adminRoleId,
                UserName = "张三"
            });

            var userAccount = await repository.FirstOrDefaultAsync<UserAccount>(x => x.Id == account.Id).ConfigureAwait(false);
            var roleUser = await repository.FirstOrDefaultAsync<RoleUser>(x => x.UserId == account.Id).ConfigureAwait(false);

            userAccount.ShouldBeNull();
            roleUser.ShouldBeNull();
        });
    }
    
    [Fact]
    public async Task CanGetUserAccountInfoAsync()
    {
        var account = await _accountUtil.AddUserAccount("张三", "123456").ConfigureAwait(false);

        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            var userAccountInfo = await mediator
                .RequestAsync<GetUserAccountInfoRequest, GetUserAccountInfoResponse>(new GetUserAccountInfoRequest { UserId = account.Id })
                .ConfigureAwait(false);
          
            userAccountInfo.Data.UserId.ShouldBe(account.Id);
            userAccountInfo.Data.UserName.ShouldBe("张三");
            userAccountInfo.Data.PassWord.ShouldBe("123456");
        });
    }
    
    [Fact]
    public async Task CanGetRolesAsync()
    {
        var userRoleId = await _securityUtil.AddRolesAsync("User").ConfigureAwait(false);
        var adminRoleId = await _securityUtil.AddRolesAsync("Administrator").ConfigureAwait(false);
        
        
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            var currentUserRoles = await mediator.RequestAsync<GetRolesRequest, GetRolesResponse>(new GetRolesRequest{ SystemSource = 0 }).ConfigureAwait(false);
            
            currentUserRoles.Data.Count.ShouldBe(2);
            currentUserRoles.Data.Roles[0].Id.ShouldBe(adminRoleId);
            currentUserRoles.Data.Roles[0].Name.ShouldBe("Administrator");
            currentUserRoles.Data.Roles[1].Id.ShouldBe(userRoleId);
            currentUserRoles.Data.Roles[1].Name.ShouldBe("User");
        });
    }
}