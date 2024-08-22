using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.System;
using SmartTalk.Core.Services.Wiltechs;
using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Requests.Account;

namespace SmartTalk.Core.Services.Account;

public interface IAccountService : IScopedDependency
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    
    Task<UserAccountDto> GetOrCreateUserAccountFromThirdPartyAsync(string userId, string userName, UserAccountIssuer issuer, CancellationToken cancellationToken);
}

public partial class AccountService : IAccountService
{
    private readonly IMapper _mapper;
    private readonly ITokenProvider _tokenProvider;
    private readonly IWiltechsService _wiltechsService;
    private readonly IAccountDataProvider _accountDataProvider;
    private readonly IVerificationCodeService _verificationCodeService;
    
    public AccountService(
        IMapper mapper, ITokenProvider tokenProvider, IWiltechsService wiltechsService, IAccountDataProvider accountDataProvider, IVerificationCodeService verificationCodeService)
    {
        _mapper = mapper;
        _tokenProvider = tokenProvider;
        _wiltechsService = wiltechsService;
        _accountDataProvider = accountDataProvider;
        _verificationCodeService = verificationCodeService;
    }
}