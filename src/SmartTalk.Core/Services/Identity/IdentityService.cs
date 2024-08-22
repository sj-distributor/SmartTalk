using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Dto.Account;

namespace SmartTalk.Core.Services.Identity;

public class IdentityService : IIdentityService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAccountDataProvider _accountDataProvider;

    public IdentityService(ICurrentUser currentUser, IAccountDataProvider accountDataProvider)
    {
        _currentUser = currentUser;
        _accountDataProvider = accountDataProvider;
    }

    public async Task<UserAccountDto> GetCurrentUserAsync(bool throwWhenNotFound = false, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.Id;

        if (userId != null)
            return await _accountDataProvider.GetUserAccountDtoAsync(userId.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (throwWhenNotFound)
            throw new UnauthorizedAccessException();
        
        return null;
    }
}