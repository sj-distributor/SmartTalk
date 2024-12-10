﻿using System.Reflection;
using Serilog;
using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Attributes;
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
        {
            var (count, account) = await _accountDataProvider.GetUserAccountDtoAsync(userId.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
         
            return account.FirstOrDefault() != null ? account.FirstOrDefault() : null;
        }
        
        if (throwWhenNotFound)
            throw new UnauthorizedAccessException();
        
        return null;
    }
    
    public async Task<bool> IsInRolesAsync(int? userId, IEnumerable<string> requiredRolesOrPermissions, CancellationToken cancellationToken)
    {
        Log.Information("IsInRolesAsync start time: {datetime}", DateTime.Now.ToString("HH:mm:ss zz"));
        
        var (count, users) = await _accountDataProvider.GetUserAccountAsync(userId, includeRoles: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var user = users.FirstOrDefault();

        if (user == null) return false;
        
        if (!user.Roles.Any() && !user.Permissions.Any()) return false;
        
        var userRoles = user.Roles.Select(x => x.Name).ToList();
        
        var userPermissions = user.Permissions.Select(x => x.Name).ToList();

        var userRolesOrPermissions = userRoles.Concat(userPermissions).ToList();

        Log.Information("IsInRolesAsync end time: {datetime}", DateTime.Now.ToString("HH:mm:ss zz"));
        
        return requiredRolesOrPermissions.All(requiredRolesOrPermission =>
            userRolesOrPermissions.Contains(requiredRolesOrPermission));
    }
    
    public (List<string> RequiredRoles, List<string> RequiredPermissions) GetRolesAndPermissionsFromAttributes(Type messageType)
    {
        Log.Information("GetRolesAndPermissionsFromAttributes start time: {datetime}", DateTime.Now.ToString("HH:mm:ss zz"));
        
        var authorizeAttributes = messageType.GetCustomAttributes<SmartTalkAuthorizeAttribute>().ToList();
        
        var roles = authorizeAttributes
            .Where(x => x.Roles != null && x.Roles.Any())
            .SelectMany(x => x.Roles).ToList();
        
        var permissions = authorizeAttributes
            .Where(x => x.Permissions != null && x.Permissions.Any())
            .SelectMany(x => x.Permissions).ToList();
        
        Log.Information("GetRolesAndPermissionsFromAttributes end time: {datetime}", DateTime.Now.ToString("HH:mm:ss zz"));
        
        return (roles, permissions);
    }

    public async Task<bool> IsCurrentUserExistAsync(int id, CancellationToken cancellationToken)
    {
        var users = await _accountDataProvider.IsUserAccountExistAsync(id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return users is not null;
    }
}