using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.Account;

namespace SmartTalk.Core.Services.Identity;

public interface IIdentityService : IScopedDependency
{
    Task<UserAccountDto> GetCurrentUserAsync(bool throwWhenNotFound = false, CancellationToken cancellationToken = default);
    
    Task<bool> IsInRolesAsync(int? userId, IEnumerable<string> requiredRolesOrPermissions, CancellationToken cancellationToken);
    
    public (List<string> RequiredRoles, List<string> RequiredPermissions) GetRolesAndPermissionsFromAttributes(Type messageType);

    Task<bool> IsCurrentUserExistAsync(int id, CancellationToken cancellationToken);
}