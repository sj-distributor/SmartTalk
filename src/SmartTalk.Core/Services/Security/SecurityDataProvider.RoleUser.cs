using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Messages.DTO.Security;

namespace SmartTalk.Core.Services.Security;

public partial class SecurityDataProvider
{
    public async Task<(int, List<RoleUser>)> GetRoleUsersPagingAsync(
        int? pageIndex = null, int? pageSize = null, int? roleId = null, string keyword = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<RoleUser>();
        
        if (roleId.HasValue)
            query = query.Where(x => x.RoleId == roleId);
        
        if (!string.IsNullOrEmpty(keyword))
        {
            query = from roleUser in query
                join user in _repository.Query<UserAccount>() on roleUser.UserId equals user.Id
                where user.UserName.Contains(keyword)
                select roleUser;
        }
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue)
        {
            query = query.OrderByDescending(x => x.CreatedOn).Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        }

        var roleUsers = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, roleUsers);
    }

    public async Task<List<RoleUserDto>> GetRoleUsersAsync(
        int? roleId = null, string keyword = null, CancellationToken cancellationToken = default)
    {
        var query =
            from roleUser in _repository.Query<RoleUser>()
            join user in _repository.Query<UserAccount>() on roleUser.UserId equals user.Id
            select new RoleUserDto
            {
                Id = roleUser.Id,
                RoleId = roleUser.RoleId,
                UserId = roleUser.UserId,
                UserName = user.UserName,
                CreatedDate = roleUser.CreatedOn,
                ModifiedDate = roleUser.ModifiedOn
            };
        
        if (roleId.HasValue)
            query = query.Where(x => x.RoleId == roleId);
        
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(x => x.UserName.Contains(keyword));
        }
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<RoleUser>> GetRoleUserByPermissionNameAsync(string permissionName, CancellationToken cancellationToken)
    {
        return await (
                from p in _repository.Query<Permission>()
                where p.Name == permissionName
                join rp in _repository.Query<RolePermission>() on p.Id equals rp.PermissionId
                join ru in _repository.Query<RoleUser>() on rp.RoleId equals ru.RoleId
                select ru
            ).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateRoleUsersAsync(
        List<RoleUser> roleUsers, CancellationToken cancellationToken, bool forceSave = true)
    {
        await _repository.InsertAllAsync(roleUsers, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRoleUsersAsync(
        List<RoleUser> roleUsers, CancellationToken cancellationToken)
    {
        await _repository.DeleteAllAsync(roleUsers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    public async Task UpdateRoleUsersAsync(
        List<RoleUser> roleUsers, CancellationToken cancellationToken, bool forceSave = true)
    {
        if (roleUsers != null)
        {
            await _repository.UpdateAllAsync(roleUsers, cancellationToken).ConfigureAwait(false);

            if (forceSave)
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    
    public async Task<List<RoleUser>> GetRoleUsersByIdsAsync(
        List<int> roleUsersIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<RoleUser>()
            .Where(x => roleUsersIds.Contains(x.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}