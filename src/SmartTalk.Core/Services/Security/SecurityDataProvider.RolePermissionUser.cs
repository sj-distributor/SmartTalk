using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Security;

namespace SmartTalk.Core.Services.Security;

public partial class SecurityDataProvider : ISecurityDataProvider
{
    public async Task CreateRolePermissionUsersAsync(
        List<RolePermissionUser> rolePermissionUsers, CancellationToken cancellationToken, bool forceSave = true)
    {
        await _repository.InsertAllAsync(rolePermissionUsers, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRolePermissionUsersAsync(
        List<RolePermissionUser> rolePermissionUsers, CancellationToken cancellationToken = default, bool forceSave = true)
    {
        await _repository.DeleteAllAsync(rolePermissionUsers, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateRolePermissionUsersAsync(
        List<RolePermissionUser> rolePermissionUsers, CancellationToken cancellationToken, bool forceSave = true)
    {
       await _repository.UpdateAllAsync(rolePermissionUsers, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int, List<RolePermissionUser>)> GetRolePermissionUsersPagingAsync(
        int pageIndex, int pageSize, int? roleId = null, int? permissionId = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<RolePermissionUser>();

        if (roleId.HasValue)
            query = query.Where(x => x.RoleId == roleId);
        
        if (permissionId.HasValue)
            query = query.Where(x => x.PermissionId == permissionId);
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        var rolePermissionUnits = await query.OrderByDescending(x => x.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, rolePermissionUnits);
    }

    public async Task<List<RolePermissionUser>> GetRolePermissionUsersAsync(
        int? roleId = null, int? permissionId = null, List<int> permissionIds = null, List<int> roleIds = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<RolePermissionUser>();

        if (roleId.HasValue)
            query = query.Where(x => x.RoleId == roleId);
        
        if (permissionId.HasValue)
            query = query.Where(x => x.PermissionId == permissionId);

        if (permissionIds is not { Count: 0 })
            query = query.Where(x => permissionIds.Contains(x.PermissionId));

        if (roleIds is not { Count: 0 })
            query = query.Where(x => roleIds.Contains(x.RoleId));
        
        return await query.OrderByDescending(x => x.CreatedDate).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RoleUser> GetRoleUserByIdAsync(int roleId, int userId, CancellationToken cancellationToken)
    {
        return await _repository.Query<RoleUser>()
            .Where(x => x.RoleId == roleId && x.UserId == userId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}