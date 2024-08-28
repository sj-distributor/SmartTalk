using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.Account;

namespace SmartTalk.Core.Services.Identity;

public interface IIdentityService : IScopedDependency
{
    Task<UserAccountDto> GetCurrentUserAsync(bool throwWhenNotFound = false, CancellationToken cancellationToken = default);
}