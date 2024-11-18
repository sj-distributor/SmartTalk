using AutoMapper;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Security;
using SmartTalk.Core.Services.System;
using SmartTalk.Core.Services.Wiltechs;
using SmartTalk.Messages.Commands.Account;
using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Requests.Account;

namespace SmartTalk.Core.Services.Account;

public interface IAccountService : IScopedDependency
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    
    Task<UserAccountDto> GetOrCreateUserAccountFromThirdPartyAsync(string userId, string userName, UserAccountIssuer issuer, CancellationToken cancellationToken);

    Task<CreateResponse> CreateUserAccount(CreateCommand command, CancellationToken cancellationToken);
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
        var userRole = await _securityDataProvider.GetRolesAsync(null, null, userId: _currentUser.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (userRole.Select(x => x.Name).Contains(command.Roles.GetDescription())) return null;
        
        var account = await _accountDataProvider.CreateUserAccountAsync(
            command.UserName, command.OriginalPassword, null, 
            UserAccountIssuer.Self, null, cancellationToken).ConfigureAwait(false);

        var role = (await _securityDataProvider.GetRolesAsync([0], cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault(x => x.Name == command.Roles.GetDescription());
        
        if (role == null) return null; 
            
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
}