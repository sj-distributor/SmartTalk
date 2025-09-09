using System.Net;
using Microsoft.AspNetCore.Http;
using Serilog;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.System;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Requests.Account;

namespace SmartTalk.Core.Services.Account;

public partial class AccountService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var authenticateResult = 
            await AuthenticateAsync(request.UserName, request.Password, request.VerificationType, cancellationToken).ConfigureAwait(false);

        if (authenticateResult.CannotLoginReason != UserAccountCannotLoginReason.None)
        {
            var code = HttpStatusCode.Unauthorized;

            if (authenticateResult.CannotLoginReason == UserAccountCannotLoginReason.NoAssociatedStore || authenticateResult.CannotLoginReason == UserAccountCannotLoginReason.IncorrectDomain)
            {
                code = HttpStatusCode.Forbidden;
            }
            
            return new LoginResponse { Code = code, Msg = GetFriendlyErrorMessage(authenticateResult.CannotLoginReason), VerifyCodeResult = authenticateResult.VerifyCodeResult };
        }
        
        return new LoginResponse
        {
            Data = authenticateResult.AccessToken
        };
    }
    
    private async Task<AuthenticateInternalResult> 
        AuthenticateAsync(string username, string clearTextPassword, UserAccountVerificationType loginVerificationType, CancellationToken cancellationToken)
    {
        AuthenticateInternalResult authenticateInternalResult = new();
        
        await AuthenticateSelfAsync(authenticateInternalResult, username, clearTextPassword, loginVerificationType, cancellationToken).ConfigureAwait(false);
        
        if (authenticateInternalResult.CannotLoginReason != UserAccountCannotLoginReason.None)
            return authenticateInternalResult;
        
        await AuthenticateWiltechsAsync(authenticateInternalResult, username, clearTextPassword, loginVerificationType, cancellationToken).ConfigureAwait(false);

        return authenticateInternalResult;
    }
    
    private async Task AuthenticateSelfAsync(
        AuthenticateInternalResult authenticateInternalResult,
        string username, string clearTextPassword, UserAccountVerificationType loginVerificationType, CancellationToken cancellationToken)
    {
        if (authenticateInternalResult.IsAuthenticated) return;

        authenticateInternalResult.CannotLoginReason = UserAccountCannotLoginReason.None;
        
        var (count, accounts) = loginVerificationType switch
        {
            UserAccountVerificationType.Password => 
                await _accountDataProvider.GetUserAccountDtoAsync(
                    username: username, password: clearTextPassword.ToSha256(), issuer: UserAccountIssuer.Self, includeRoles: true, cancellationToken: cancellationToken).ConfigureAwait(false),
            UserAccountVerificationType.VerificationCode => 
                await _accountDataProvider.GetUserAccountDtoAsync(
                    username: username, issuer: UserAccountIssuer.Self, includeRoles: true, cancellationToken: cancellationToken).ConfigureAwait(false)
        };

        var account = accounts?.FirstOrDefault();

        if (account == null)
        {
            authenticateInternalResult.CannotLoginReason = UserAccountCannotLoginReason.NotFound;
            authenticateInternalResult.IsAuthenticated = false;
            return;            
        }
        
        var httpContext = _httpContextAccessor.HttpContext;
        var currentDomain = httpContext?.Request.Headers.Origin.ToString();
        
        Log.Information("The domain is: {Domain}", currentDomain);
        var domain = httpContext?.Request.Headers.Origin.ToString();
        
        Log.Information("The domain is: {Domain}", domain);

        if (!string.IsNullOrEmpty(currentDomain))
        {
            var allServiceProviders = await _posDataProvider.GetServiceProviderByIdAsync(null, cancellationToken).ConfigureAwait(false);
            
            var registeredDomains = allServiceProviders?
                .Where(sp => !string.IsNullOrEmpty(sp.Domain))
                .Select(sp => sp.Domain!.Trim())
                .ToList() ?? new List<string>();

            if (registeredDomains.Contains(currentDomain, StringComparer.OrdinalIgnoreCase))
            {
                var userServiceProvider = await _posDataProvider.GetServiceProviderByIdAsync(account.ServiceProviderId, cancellationToken).ConfigureAwait(false);
                
                var userDomains = userServiceProvider?
                    .Where(sp => !string.IsNullOrEmpty(sp.Domain))
                    .Select(sp => sp.Domain!.Trim())
                    .ToList() ?? new List<string>();

                if (!userDomains.Contains(currentDomain, StringComparer.OrdinalIgnoreCase))
                {
                    authenticateInternalResult.CannotLoginReason = UserAccountCannotLoginReason.IncorrectDomain;
                    authenticateInternalResult.IsAuthenticated = false;
                    return;
                }
            }
        }
        
        if (loginVerificationType == UserAccountVerificationType.Password)
        {
            if (account.AccountLevel == UserAccountLevel.AiAgent || account.AccountLevel == UserAccountLevel.Company)
            {
                var storeUsers = await _posDataProvider.GetPosStoreUsersByUserIdAsync(account.Id, cancellationToken).ConfigureAwait(false);
                if (!storeUsers.Any())
                {
                    authenticateInternalResult.CannotLoginReason = UserAccountCannotLoginReason.NoAssociatedStore;
                    authenticateInternalResult.IsAuthenticated = false;
                    return;
                }
            }
        }

        switch (loginVerificationType)
        {
            case UserAccountVerificationType.Password:
                break;
            case UserAccountVerificationType.VerificationCode:
                authenticateInternalResult.VerifyCodeResult = await _verificationCodeService.VerifyAsync(new VerifyCodeParams
                {
                    Identity = username,
                    Recipient = username,
                    Code = clearTextPassword
                }, cancellationToken).ConfigureAwait(false);
                if (!authenticateInternalResult.VerifyCodeResult.IsValid)
                {
                    authenticateInternalResult.CannotLoginReason = UserAccountCannotLoginReason.VerificationCodeInvalid;     
                    return;                    
                }
                break;
        }

        var accessToken = _tokenProvider.Generate(_accountDataProvider.GenerateClaimsFromUserAccount(account));

        authenticateInternalResult.AccessToken = accessToken;
        authenticateInternalResult.IsAuthenticated = !string.IsNullOrEmpty(accessToken);
    }
    
    private async Task AuthenticateWiltechsAsync(
        AuthenticateInternalResult authenticateInternalResult,
        string username, string clearTextPassword, UserAccountVerificationType loginVerificationType, CancellationToken cancellationToken)
    {
        if (authenticateInternalResult.IsAuthenticated) return;

        authenticateInternalResult.CannotLoginReason = UserAccountCannotLoginReason.None;
        
        var (accessToken, account) = await GetOrCreateUserAccountFromWiltechsAsync(username, clearTextPassword, cancellationToken).ConfigureAwait(false);

        if (account == null)
        {
            authenticateInternalResult.CannotLoginReason = UserAccountCannotLoginReason.NotFound;
            return;            
        }

        authenticateInternalResult.AccessToken = accessToken;
        authenticateInternalResult.IsAuthenticated = !string.IsNullOrEmpty(accessToken);
    }
    
    private class AuthenticateInternalResult
    {
        public bool IsAuthenticated { get; set; }
        
        public string AccessToken { get; set; }
        
        public VerifyCodeResult VerifyCodeResult { get; set; }

        public UserAccountCannotLoginReason CannotLoginReason { get; set; } = UserAccountCannotLoginReason.None;
    }
    
    private string GetFriendlyErrorMessage(UserAccountCannotLoginReason reason) => reason switch
    {
        UserAccountCannotLoginReason.NoAssociatedStore => "The account is not associated with the store, please contact the administrator",
        
        UserAccountCannotLoginReason.IncorrectDomain => "Unable to login, please use the correct domain name to access",
    
        _ => reason.ToString()
    };
}