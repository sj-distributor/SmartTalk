using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.Security;

namespace SmartTalk.Core.Services.Security;

public partial class SecurityDataProvider
{
    public async Task CreateUserPermissionsAsync(List<UserPermission> userPermissions, CancellationToken cancellationToken, bool forceSave = true)
    {
        await _repository.InsertAllAsync(userPermissions, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task UpdateUserPermissionsAsync(List<UserPermission> userPermissions, CancellationToken cancellationToken, bool forceSave = true)
    {
        if (userPermissions != null)
        {
            await _repository.UpdateAllAsync(userPermissions, cancellationToken).ConfigureAwait(false);

            if (forceSave)
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    
    public async Task DeleteUserPermissionsAsync(List<UserPermission> userPermissions, CancellationToken cancellationToken)
    {
        await _repository.DeleteAllAsync(userPermissions, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<UserPermission>> GetUserPermissionsByIdsAsync(List<int> userPermissionsIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<UserPermission>()
            .Where(x => userPermissionsIds.Contains(x.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<(int, List<UserPermission>)> GetUserPermissionsAsync(
        int pageIndex, int pageSize, string keyword, CancellationToken cancellationToken)
    {
        var query = _repository.Query<UserPermission>();
        
        if (!string.IsNullOrEmpty(keyword))
        {
            query = from userPermission in query
                join user in _repository.Query<UserAccount>() on userPermission.UserId equals user.Id
                where user.UserName.Contains(keyword)
                select userPermission;
        }
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var userPermissions = await query
            .OrderByDescending(up => up.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (count, userPermissions);
    }
}