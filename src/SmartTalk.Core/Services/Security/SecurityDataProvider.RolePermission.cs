using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Messages.Enums.Security;

namespace SmartTalk.Core.Services.Security;

public partial class SecurityDataProvider
{
    public async Task CreateRolePermissionsAsync(
        List<RolePermission> rolePermissions, CancellationToken cancellationToken, bool forceSave = true)
    {
        await _repository.InsertAllAsync(rolePermissions, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task UpdateRolePermissionsAsync(
        List<RolePermission> rolePermissions, CancellationToken cancellationToken, bool forceSave = true)
    {
        if (rolePermissions != null)
        {
            await _repository.UpdateAllAsync(rolePermissions, cancellationToken).ConfigureAwait(false);

            if (forceSave)
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    
    public async Task DeleteRolePermissionsAsync(
        List<RolePermission> rolePermissions, CancellationToken cancellationToken)
    {
        await _repository.DeleteAllAsync(rolePermissions, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<RolePermission>> GetRolePermissionsByIdsAsync(
        List<int> rolePermissionsIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<RolePermission>()
            .Where(x => rolePermissionsIds.Contains(x.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<RolePermission>> GetRolePermissionsByRoleIdsAsync(List<int> roleIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<RolePermission>()
            .Where(x => roleIds.Contains(x.RoleId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<(int, List<RolePermission>)> GetRolePermissionsAsync(
        int pageIndex, int pageSize, string keyword = null, RoleSystemSource? systemSource = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<RolePermission>();
        
        if (!string.IsNullOrEmpty(keyword))
        {
            query = from rolePermission in query
                join role in _repository.Query<Role>() on rolePermission.RoleId equals role.Id
                where role.Name.Contains(keyword) || role.DisplayName.Contains(keyword)
                select rolePermission;
        }
        
        if (systemSource.HasValue)
        {
            query = from rolePermission in query
                join role in _repository.Query<Role>() on rolePermission.RoleId equals role.Id
                where role.SystemSource == systemSource || role.SystemSource == RoleSystemSource.System
                select rolePermission;
        }
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        var rolePermissions = await query
            .OrderByDescending(x => x.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        
        return (count, rolePermissions);
    }

    public async Task<List<RolePermission>> GetRolePermissionsByUserIdAsync(
        int userId, CancellationToken cancellationToken)
    {
        return await (from roleUser in _repository.Query<RoleUser>()
            join rolePermission in _repository.Query<RolePermission>() on roleUser.RoleId equals rolePermission.RoleId
            where roleUser.UserId == userId
            select rolePermission).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<RolePermission>> GetRolePermissionsByUserNameAsync(
        string userName, CancellationToken cancellationToken = default)
    {
        var query =
            from account in _repository.Query<UserAccount>()
            join roleUser in _repository.Query<RoleUser>() on account.Id equals roleUser.UserId
            join rolePermission in _repository.Query<RolePermission>() on roleUser.RoleId equals rolePermission.RoleId
            where account.UserName == userName && account.IsActive
            select rolePermission;
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int Count, List<RolePermission> RolePermissions)> GetRolePermissionsWithPagesByRoleAsync(
        int? pageIndex = null, int? pageSize = null, string keyword = null, int? userId = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<RolePermission>();
        
        if (!string.IsNullOrEmpty(keyword))
        {
            query = from rolePermission in query
                join role in _repository.Query<Role>() on rolePermission.RoleId equals role.Id
                where role.Name.Contains(keyword)
                select rolePermission;

            var count = await _repository.Query<Role>().CountAsync(cancellationToken).ConfigureAwait(false);
            
            if (pageIndex.HasValue && pageSize.HasValue)
            {
                query =  query.OrderByDescending(x => x.CreatedDate).Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
            }
            
            var rolePermissions = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            
            return (count, rolePermissions);
        }

        if (pageIndex.HasValue && pageSize.HasValue && keyword == null)
        {
            var count = await _repository.Query<Role>().CountAsync(cancellationToken).ConfigureAwait(false);
            
            var roleIds = _repository.Query<Role>()
                .OrderByDescending(x => x.CreatedOn)
                .Skip((pageIndex.Value - 1) * pageSize.Value)
                .Take(pageSize.Value).Select(x => x.Id);

            query = query.Where(x => roleIds.Contains(x.RoleId));
            
            var rolePermissions = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            
            return (count, rolePermissions);
        }

        query = from roleUser in _repository.Query<RoleUser>()
            join rolePermission in query on roleUser.RoleId equals rolePermission.RoleId
            where roleUser.UserId == userId
            select rolePermission;
        
        return (await query.CountAsync(cancellationToken).ConfigureAwait(false),
            await query.ToListAsync(cancellationToken).ConfigureAwait(false));
    }
    
    public async Task GrantPermissionsIntoRoleAsync(
        List<Permission> permissions, Role role, CancellationToken cancellationToken)
    {
        var permissionIds = permissions.Select(x => x.Id).ToList();
        
        var currentRolePermissions = await _repository.Query<RolePermission>()
            .Where(x => role.Id == x.RoleId &&permissionIds.Contains(x.PermissionId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        
        var newRolePermissions = permissions
            .Select(x => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = x.Id
            }).ToList();
        
        await _repository.DeleteAllAsync(currentRolePermissions, cancellationToken).ConfigureAwait(false);
        await _repository.InsertAllAsync(newRolePermissions, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}