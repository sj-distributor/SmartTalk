using SmartTalk.Core.Services.Account.Exceptions;
using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.Core.Services.Account;

public partial class AccountService
{
    public async Task<(string AccessToken, UserAccountDto UserAccount)> GetOrCreateUserAccountFromWiltechsAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return (null, null);
        
        var wiltechsUser = await _wiltechsService.AuthenticateAsync(username, password, cancellationToken).ConfigureAwait(false);
        
        if (wiltechsUser?.UserId == Guid.Empty || string.IsNullOrEmpty(wiltechsUser?.Username)) return (null, null);
        
        return (wiltechsUser.AccessToken, await GetOrCreateUserAccountFromThirdPartyAsync(wiltechsUser.UserId.ToString(), wiltechsUser.Username, UserAccountIssuer.Wiltechs, cancellationToken).ConfigureAwait(false));
    }
    
    public async Task<UserAccountDto> GetOrCreateUserAccountFromThirdPartyAsync(string userId, string username, UserAccountIssuer issuer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
            throw new UserAccountColumnsRequiredException(nameof(userId), nameof(username));
        
        var (count, userAccount) = await _accountDataProvider.GetUserAccountDtoAsync(thirdPartyUserId: userId, includeRoles: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (userAccount is not { Count: 0 }) return userAccount.FirstOrDefault();

        var currentUser = await _accountDataProvider.GetUserAccountByUserIdAsync(_currentUser.Id.Value, cancellationToken: cancellationToken).ConfigureAwait(false);

        var account = await _accountDataProvider
            .CreateUserAccountAsync(username, null, UserAccountLevel.None, currentUser.ServiceProviderId, userId, issuer, cancellationToken: cancellationToken).ConfigureAwait(false);

        return _mapper.Map<UserAccountDto>(account);
    }
}