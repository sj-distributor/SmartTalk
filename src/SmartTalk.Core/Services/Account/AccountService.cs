using AutoMapper;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Security;
using SmartTalk.Core.Services.System;
using SmartTalk.Core.Services.Wiltechs;
using SmartTalk.Messages.Commands.Account;
using SmartTalk.Messages.Commands.Authority;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Requests.Account;

namespace SmartTalk.Core.Services.Account;

public interface IAccountService : IScopedDependency
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    
    Task<UserAccountDto> GetOrCreateUserAccountFromThirdPartyAsync(string userId, string userName, UserAccountIssuer issuer, CancellationToken cancellationToken);

    Task<CreateResponse> CreateUserAccount(CreateCommand command, CancellationToken cancellationToken);

    Task<GetAccountsResponse> GetAccountsAsync(GetAccountsCommand command, CancellationToken cancellationToken);

    Task<DeleteAccountsResponse> DeleteUserAccountAsync(DeleteAccountsCommand command, CancellationToken cancellationToken);
}

public partial class AccountService : IAccountService
{
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly ITokenProvider _tokenProvider;
    private readonly IWiltechsService _wiltechsService;
    private readonly IAccountDataProvider _accountDataProvider;
    private readonly ISecurityDataProvider _securityDataProvider;
    private readonly IVerificationCodeService _verificationCodeService;
    
    public AccountService(
        IMapper mapper, ICurrentUser currentUser, ITokenProvider tokenProvider, IWiltechsService wiltechsService, IAccountDataProvider accountDataProvider, ISecurityDataProvider securityDataProvider, IVerificationCodeService verificationCodeService)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _tokenProvider = tokenProvider;
        _wiltechsService = wiltechsService;
        _accountDataProvider = accountDataProvider;
        _securityDataProvider = securityDataProvider;
        _verificationCodeService = verificationCodeService;
    }

    public async Task<CreateResponse> CreateUserAccount(CreateCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(command.RoleName) || string.IsNullOrEmpty(command.UserName)) return new CreateResponse();

        var role = (await _securityDataProvider.GetRolesAsync([0], name: command.RoleName, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        if (role == null) return null;
        
        var account = await _accountDataProvider.CreateUserAccountAsync(
            command.UserName, command.OriginalPassword, null, 
            UserAccountIssuer.Self, null, cancellationToken).ConfigureAwait(false);
            
        var roleUser = new RoleUser
        {
            RoleId = role.Id,
            UserId = account.Id
        };
        
        await _securityDataProvider.CreateRoleUsersAsync([roleUser], cancellationToken).ConfigureAwait(false);
        
        return new CreateResponse
        {
            Data = _mapper.Map<UserAccountDto>(account)
        };
    }

    public async Task<GetAccountsResponse> GetAccountsAsync(GetAccountsCommand command, CancellationToken cancellationToken)
    {
        var userAccount = await _accountDataProvider.GetUserAccountDtoAsync(userNameContain: command.UserName, pageSize: command.PageSize, pageIndex: command.PageIndex, includeRoles: true, creator: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GetAccountsResponse
        {
            Data = userAccount
        };
    }
    
    public async Task<DeleteAccountsResponse> DeleteUserAccountAsync(DeleteAccountsCommand command, CancellationToken cancellationToken)
    {
        var account = (await _accountDataProvider.GetUserAccountAsync(username: command.UserName, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        var accountProfile = await _accountDataProvider.GetUserAccountProfileAsync(account.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var (count, roleUsers) = await _securityDataProvider.GetRoleUsersPagingAsync(roleId: command.roleId, keyword: command.UserName , cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await _accountDataProvider.DeleteUserAccountProfileAsync(accountProfile, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await _accountDataProvider.DeleteUserAccountAsync(account, true, cancellationToken).ConfigureAwait(false);
        
        await _securityDataProvider.DeleteRoleUsersAsync(roleUsers, cancellationToken).ConfigureAwait(false);
        
        return new DeleteAccountsResponse();
    }
}