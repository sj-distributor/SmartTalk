using AutoMapper;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.System;
using SmartTalk.Messages.Dto.Account;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Security;
using SmartTalk.Core.Services.Wiltechs;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Requests.Account;
using SmartTalk.Messages.Commands.Security;
using SmartTalk.Messages.Requests.Security;

namespace SmartTalk.Core.Services.Account;

public interface IAccountService : IScopedDependency
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    
    Task<UserAccountDto> GetOrCreateUserAccountFromThirdPartyAsync(string userId, string userName, UserAccountIssuer issuer, CancellationToken cancellationToken);

    Task<CreateUserAccountResponse> CreateUserAccountAsync(CreateUserAccountCommand userAccountCommand, CancellationToken cancellationToken);

    Task<GetUserAccountsResponse> GetAccountsAsync(GetUserAccountsRequest request, CancellationToken cancellationToken);

    Task<DeleteUserAccountsResponse> DeleteUserAccountAsync(DeleteUserAccountsCommand command, CancellationToken cancellationToken);
    
    Task<GetUserAccountInfoResponse> GetAccountInfoAsync(GetUserAccountInfoRequest request, CancellationToken cancellationToken);
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
    private readonly IPosDataProvider _posDataProvider;
    
    public AccountService(
        IMapper mapper, ICurrentUser currentUser, ITokenProvider tokenProvider, IWiltechsService wiltechsService, IAccountDataProvider accountDataProvider, ISecurityDataProvider securityDataProvider, IVerificationCodeService verificationCodeService
        ,IPosDataProvider posDataProvider)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _tokenProvider = tokenProvider;
        _wiltechsService = wiltechsService;
        _accountDataProvider = accountDataProvider;
        _securityDataProvider = securityDataProvider;
        _verificationCodeService = verificationCodeService;
        _posDataProvider = posDataProvider;
    }

    public async Task<CreateUserAccountResponse> CreateUserAccountAsync(CreateUserAccountCommand userAccountCommand, CancellationToken cancellationToken)
    {
        var existAccount = await _accountDataProvider.GetUserAccountAsync(username: userAccountCommand.UserName, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (existAccount.Item1 > 0)
            throw new Exception("The name is already in use and cannot be created");
        
        var account = await _accountDataProvider.CreateUserAccountAsync(
            userAccountCommand.UserName, userAccountCommand.OriginalPassword, userAccountCommand.AccountLevel, null,
            UserAccountIssuer.Self, null, _currentUser.Name, isProfile: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await _securityDataProvider.CreateRoleUsersAsync([new RoleUser
        {
            RoleId = userAccountCommand.RoleId,
            UserId = account.Id
        }], cancellationToken).ConfigureAwait(false);

        var stores = await _posDataProvider.GetPosCompanyStoresAsync(companyIds: userAccountCommand.CompanyIds, ids: userAccountCommand.StoreIds, cancellationToken: cancellationToken).ConfigureAwait(false);

        var storeUsers = stores.Select(store => new PosStoreUser
        {
            UserId = account.Id,
            StoreId = store.Id
        }).ToList();

        await _posDataProvider.CreatePosStoreUserAsync(storeUsers, forceSave: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new CreateUserAccountResponse
        {
            Data = _mapper.Map<UserAccountDto>(account)
        };
    }

    public async Task<GetUserAccountsResponse> GetAccountsAsync(GetUserAccountsRequest request, CancellationToken cancellationToken)
    {
        var (count, userAccount) = await _accountDataProvider.GetUserAccountDtoAsync(
            request.UserName, request.UserAccountLevel, request.PageSize, request.PageIndex, true, cancellationToken).ConfigureAwait(false);

        return new GetUserAccountsResponse
        {
           Data = new GetUserAccountsDto
           {
               UserAccounts = userAccount,
               Count = count
           }
        };
    }
    
    public async Task<DeleteUserAccountsResponse> DeleteUserAccountAsync(DeleteUserAccountsCommand command, CancellationToken cancellationToken)
    {
        var account = await _accountDataProvider.IsUserAccountExistAsync(command.UserId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (account is null) return null;
        
        var (count, roleUsers) = await _securityDataProvider.GetRoleUsersPagingAsync(roleId: command.RoleId, keyword: command.UserName , cancellationToken: cancellationToken).ConfigureAwait(false);

        await _accountDataProvider.DeleteUserAccountAsync(account, true, cancellationToken).ConfigureAwait(false);
        
        await _securityDataProvider.DeleteRoleUsersAsync(roleUsers, cancellationToken).ConfigureAwait(false);

        var posStoreUsers = await _posDataProvider.GetPosStoreUsersByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        await _posDataProvider.DeletePosStoreUsersAsync(posStoreUsers, true, cancellationToken).ConfigureAwait(false);
        
        return new DeleteUserAccountsResponse();
    }

    public async Task<GetUserAccountInfoResponse> GetAccountInfoAsync(GetUserAccountInfoRequest request, CancellationToken cancellationToken)
    {
        var (accountCount, accounts) = await _accountDataProvider.GetUserAccountAsync(id: request.UserId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var account = accounts.FirstOrDefault();

        if (account == null) return null;
        
        return new GetUserAccountInfoResponse
        {
           Data = new GetUserAccountInfoDto
           {
               UserId = account.Id,
               UserName = account.UserName,
               PassWord = account.OriginalPassword
           }
        };
    }
}