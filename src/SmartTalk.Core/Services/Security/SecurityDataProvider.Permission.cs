using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Messages.DTO.Security;

namespace SmartTalk.Core.Services.Security;

public partial class SecurityDataProvider
{
    public async Task CreatePermissionsAsync(List<Permission> permissions, CancellationToken cancellationToken, bool forceSave = true)
    {
        await _repository.InsertAllAsync(permissions, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task UpdatePermissionsAsync(List<Permission> permissions, CancellationToken cancellationToken, bool forceSave = true)
    {
        if (permissions != null)
        {
            await _repository.UpdateAllAsync(permissions, cancellationToken).ConfigureAwait(false);

            if (forceSave)
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    
    public async Task DeletePermissionsAsync(List<Permission> permissions, CancellationToken cancellationToken)
    {
        await _repository.DeleteAllAsync(permissions, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePermissionsRelatedAsync(List<Permission> permissions, CancellationToken cancellationToken)
    {
        var permissionIds = permissions.Select(x => x.Id);
        
        var userPermissionToDelete = await _repository.Query<UserPermission>()
            .Where(up => permissionIds.Contains(up.PermissionId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        
        var rolePermissionsToDelete = await _repository.Query<RolePermission>()
            .Where(rp => permissionIds.Contains(rp.PermissionId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        
        await _repository.DeleteAllAsync(permissions, cancellationToken).ConfigureAwait(false);
        await _repository.DeleteAllAsync(userPermissionToDelete, cancellationToken).ConfigureAwait(false);
        await _repository.DeleteAllAsync(rolePermissionsToDelete, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Permission>> GetPermissionsByIdsAsync(List<int> permissionsIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<Permission>()
            .Where(x => permissionsIds.Contains(x.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<Permission> GetPermissionByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync<Permission>(id, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<(int, List<Permission>)> GetPermissionsPagingAsync(
        List<string> names = null, string keyword = null, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<Permission>();

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Name.Contains(keyword));
        
        if (names != null && names.Any())
            query = query.Where(x => names.Contains(x.Name));

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.OrderByDescending(x => x.CreatedDate).Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        return (count, await query.ToListAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<List<Permission>> GetPermissionsAsync(
        List<string> names = null, string keyword = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<Permission>();

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Name.Contains(keyword));
        
        if (names != null && names.Any())
            query = query.Where(x => names.Contains(x.Name));
        
        return await query
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Permission>> GetPermissionsOfRolesAsync(
        List<RoleDto> roles, CancellationToken cancellationToken)
    {
        if (!roles.Any()) return new List<Permission>();

        var roleIds = roles.Select(role => role.Id).ToList();

        return await (
            from rolePermission in _repository.Query<RolePermission>()
            join permission in _repository.Query<Permission>() on rolePermission.PermissionId equals permission.Id
            where roleIds.Contains(rolePermission.RoleId)
            select permission
        ).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}