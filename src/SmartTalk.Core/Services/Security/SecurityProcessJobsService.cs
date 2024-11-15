using System.Reflection;
using SmartTalk.Core.Ioc;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Attributes;
using Microsoft.IdentityModel.Tokens;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Messages.Commands.Security;

namespace SmartTalk.Core.Services.Security;

public interface ISecurityProcessJobsService : IScopedDependency
{
    Task AutomaticPermissionsPersistAsync(AutomaticPermissionsPersistCommand command, CancellationToken cancellationToken);
}

public class SecurityProcessJobService : ISecurityProcessJobsService
{
    private readonly ISecurityDataProvider _securityProvider;
    
    public SecurityProcessJobService(ISecurityDataProvider securityProvider)
    {
        _securityProvider = securityProvider;
    }
    
    public async Task AutomaticPermissionsPersistAsync(
        AutomaticPermissionsPersistCommand command, CancellationToken cancellationToken)
    {
        var matchingTypes = typeof(CreateRolesCommand).Assembly.GetTypes()
            .Where(t => t.IsClass && (typeof(ICommand).IsAssignableFrom(t) || typeof(IRequest).IsAssignableFrom(t)));

        var requiredPermissionNames = new List<string>();

        foreach (var type in matchingTypes)
        {
            if (type.GetCustomAttribute(typeof(SmartTalkAuthorizeAttribute), false) is not SmartTalkAuthorizeAttribute classAttribute
                || classAttribute.Permissions.IsNullOrEmpty()) continue;
        
            requiredPermissionNames.AddRange(classAttribute.Permissions);
        }
        
        if (!requiredPermissionNames.Any()) return;

        var requiredPermissions = await _securityProvider
            .GetPermissionsAsync(names: requiredPermissionNames, cancellationToken: cancellationToken).ConfigureAwait(false);

        var shouldPersistPermissions = requiredPermissionNames.Except(requiredPermissions.Select(x => x.Name)).ToList();
        
        if (shouldPersistPermissions.Any())
        {
            var persistPermissions = await AddPermissionsAsync(shouldPersistPermissions, cancellationToken).ConfigureAwait(false);
            
            await GrantPermissionsIntoRoleAsync(persistPermissions, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<List<Permission>> AddPermissionsAsync(List<string> shouldPersistPermissionNames, CancellationToken cancellationToken)
    {
        var shouldPersistPermissions = shouldPersistPermissionNames.Select(permissionName => new Permission
        {
            IsSystem = true,
            Name = permissionName,
            CreatedDate = DateTimeOffset.Now,
            LastModifiedDate = DateTimeOffset.Now
        }).ToList();
        
        await _securityProvider.CreatePermissionsAsync(shouldPersistPermissions, cancellationToken).ConfigureAwait(false);

        return shouldPersistPermissions;
    }

    private async Task GrantPermissionsIntoRoleAsync(List<Permission> persistPermissions, CancellationToken cancellationToken)
    {
        var requireGrantRoles = await GetRequiredGrantRolesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var requireGrantRole in requireGrantRoles)
        {
            await _securityProvider
                .GrantPermissionsIntoRoleAsync(persistPermissions, requireGrantRole, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<List<Role>> GetRequiredGrantRolesAsync(CancellationToken cancellationToken)
    {
        return await _securityProvider.GetRolesByNameAsync(SecurityStore.Roles.Administrator, cancellationToken).ConfigureAwait(false);
    }
}