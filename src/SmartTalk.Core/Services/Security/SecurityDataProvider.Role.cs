using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.Security;

namespace SmartTalk.Core.Services.Security;

public partial interface ISecurityDataProvider
{
    Task<List<Role>> GetRolesAsync(
        List<RoleSystemSource> systemSources = null, bool? isSystem = null, int? userId  = null, string name = null, int? id = null, CancellationToken cancellationToken = default);
}

public partial class SecurityDataProvider
{
    public async Task<List<Role>> GetRolesAsync(
        List<RoleSystemSource> systemSources = null, bool? isSystem = null, int? userId  = null, string name = null, int? id = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<Role>();
        
        if (systemSources != null && systemSources.Any())
            query = query.Where(x => systemSources.Contains(x.SystemSource));
        
        if (isSystem.HasValue)
            query = query.Where(x => x.IsSystem == isSystem.Value);

        if (!string.IsNullOrEmpty(name))
            query = query.Where(x => x.Name == name);

        if (id.HasValue)
            query = query.Where(x => x.Id == id);
        
        if (userId.HasValue)
            query = from role in query
                join roleUser in _repository.Query<RoleUser>() on role.Id equals roleUser.RoleId
                where roleUser.UserId == userId.Value
                select role;
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateRolesAsync(
        List<Role> roles, CancellationToken cancellationToken, bool forceSave = true)
    {
        var uniqueRoles = await _repository.Query<Role>().Where(x => roles.Select(r => r.Name).Contains(x.Name))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (uniqueRoles.Any()) throw new Exception("该角色已存在，请勿重复创建！");

        await _repository.InsertAllAsync(roles, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task UpdateRolesAsync(
        List<Role> roles, CancellationToken cancellationToken, bool forceSave = true)
    {
        await _repository.UpdateAllAsync(roles, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task DeleteRolesAsync(
        List<Role> roles, CancellationToken cancellationToken)
    {
        await _repository.DeleteAllAsync(roles, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRolesRelatedAsync(List<Role> roles, CancellationToken cancellationToken)
    {
        var roleIds = roles.Select(x => x.Id);
        
        var roleUsersToDelete = await _repository.Query<RoleUser>()
            .Where(ru => roleIds.Contains(ru.RoleId)).ToListAsync(cancellationToken).ConfigureAwait(false);

        var rolePermissionsToDelete = await _repository.Query<RolePermission>()
            .Where(rp => roleIds.Contains(rp.RoleId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        
        var rolePermissionUsersToDelete = await _repository.Query<RolePermissionUser>()
            .Where(rpu => roleIds.Contains(rpu.RoleId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        
        await _repository.DeleteAllAsync(roles, cancellationToken).ConfigureAwait(false);
        await _repository.DeleteAllAsync(roleUsersToDelete, cancellationToken).ConfigureAwait(false);
        await _repository.DeleteAllAsync(rolePermissionsToDelete, cancellationToken).ConfigureAwait(false);
        await _repository.DeleteAllAsync(rolePermissionUsersToDelete, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Role>> GetRolesByIdsAsync(
        List<int> roleIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<Role>()
            .Where(x => roleIds.Contains(x.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<Role> GetRoleByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync<Role>(id, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<(int, List<Role>)> GetRolesAsync(
        int? pageIndex = null, int? pageSize = null, string keyword = null, int? userId = null,  RoleSystemSource? systemSource = null, UserAccountLevel? accountLevel = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<Role>();

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Name.Contains(keyword) || x.DisplayName.Contains(keyword));

        if (systemSource.HasValue)
            query = query.Where(x => x.SystemSource == systemSource.Value || x.SystemSource == RoleSystemSource.System);

        if (accountLevel.HasValue)
        {
            query = accountLevel.Value switch
            {
                UserAccountLevel.ServiceProvider => query.Where(x => x.Name == "ServiceProviderOperator" || x.Name == "ServiceProviderAdministrator"),
                UserAccountLevel.Company or UserAccountLevel.AiAgent => query.Where(x => x.Name == "Operator"),
                _ => query.Where(x => false)
            };
        }
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.OrderByDescending(x => x.CreatedOn).Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);

        if (!pageIndex.HasValue && !pageSize.HasValue && string.IsNullOrEmpty(keyword))
        {
            query = from role in query
                join roleUser in _repository.Query<RoleUser>() on role.Id equals roleUser.RoleId
                where roleUser.UserId == userId
                select role;
            
            count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        var roles = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, roles);
    }

    public async Task<List<Role>> GetRolesByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await _repository.Query<Role>().Where(x => x.Name == name).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<Role>> GetCurrentUserRolesAsync(RoleSystemSource? systemSource = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<Role>();

        if (systemSource.HasValue)
            query = query.Where(x => x.SystemSource == systemSource.Value || x.SystemSource == RoleSystemSource.System);
        
        return await (
            from roleUser in _repository.Query<RoleUser>()
            join role in query on roleUser.RoleId equals role.Id
            where _currentUser.Id == roleUser.UserId
            select role
        ).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}