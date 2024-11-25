using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Messages.Dto.Security;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Messages.Requests.Security;
using SmartTalk.Messages.Commands.Authority;

namespace SmartTalk.Core.Services.Security;

public interface ISecurityService : IScopedDependency
{
    Task<UpdateResponse> UpdateRoleUserAsync(UpdateCommand command, CancellationToken cancellationToken);
    
     Task<GetCurrentUserRolesResponse> GetCurrentUserRoleAsync(
        GetCurrentUserRolesRequest request, CancellationToken cancellationToken);
     
     Task<GetPermissionsByRoleIdResponse> GetPermissionsByRoleIdAsync(
        GetPermissionsByRoleIdRequest request, CancellationToken cancellationToken);
}

public class SecurityService : ISecurityService
{
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly ISecurityDataProvider _securityDataProvider;
    
    public SecurityService(IMapper mapper, ICurrentUser currentUser, ISecurityDataProvider securityDataProvider)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _securityDataProvider = securityDataProvider;
    }
                            
    public async Task<UpdateResponse> UpdateRoleUserAsync(UpdateCommand command, CancellationToken cancellationToken)
    {
        var roleUser = await _securityDataProvider.GetRoleUserByIdAsync(command.RoleId, command.UserId, cancellationToken).ConfigureAwait(false);
        
        var role = await _securityDataProvider.GetRolesAsync([0], name: command.RoleName, cancellationToken: cancellationToken).ConfigureAwait(false);

        roleUser.RoleId = role.FirstOrDefault()?.Id ?? command.RoleId;
        
        await _securityDataProvider.UpdateRoleUsersAsync([roleUser], cancellationToken).ConfigureAwait(false);

        return _mapper.Map<UpdateResponse>(roleUser);
    }
     
    public async Task<GetCurrentUserRolesResponse> GetCurrentUserRoleAsync(
        GetCurrentUserRolesRequest request, CancellationToken cancellationToken)
    {
        var currentRoles = await _securityDataProvider.GetCurrentUserRolesAsync(request.SystemSource, cancellationToken).ConfigureAwait(false);

        if (!currentRoles.Any()) return new GetCurrentUserRolesResponse();

        var rolePermissionData = await GetRolePermissionDataAsync(currentRoles, cancellationToken);
        
        var rolePermissionUsers = await _securityDataProvider.GetRolePermissionUsersAsync(roleIds: currentRoles.Select(x => x.Id).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GetCurrentUserRolesResponse
        {
            Data = new GetCurrentUserRolesResponseData
            {
                Count = currentRoles.Count,
                RolePermissionData = rolePermissionData,
            }
        };
    }
    
    private async Task<List<RolePermissionDataDto>> GetRolePermissionDataAsync(
        List<Role> roles, CancellationToken cancellationToken)
    {
        var roleIds = roles.Select(x => x.Id).ToList();

        var rolePermissions =
            await _securityDataProvider.GetRolePermissionsByRoleIdsAsync(roleIds, cancellationToken).ConfigureAwait(false);

        if (!rolePermissions.Any()) return new List<RolePermissionDataDto>();

        var permissions = await _securityDataProvider.GetPermissionsByIdsAsync(rolePermissions.Select(x => x.PermissionId).ToList(), cancellationToken);

        var rolePermissionData = (from role in roles
            let permissionsIds = rolePermissions.Where(x => x.RoleId == role.Id)
                .Select(x => x.PermissionId)
                .ToList()
            let matchRole = roles.FirstOrDefault(x => x.Id == role.Id)
            let matchPermissions = permissions.Where(x => permissionsIds.Contains(x.Id)).ToList()
            select new RolePermissionDataDto
            {
                Role = _mapper.Map<RoleDto>(matchRole),
                Permissions = _mapper.Map<List<PermissionDto>>(matchPermissions)
            }).ToList();

        return rolePermissionData;
    }
    
     public async Task<GetPermissionsByRoleIdResponse> GetPermissionsByRoleIdAsync(
        GetPermissionsByRoleIdRequest request, CancellationToken cancellationToken)
    {
        var role = await _securityDataProvider.GetRoleByIdAsync(request.RoleId, cancellationToken).ConfigureAwait(false);
        
        var rolePermissions = 
            await _securityDataProvider.GetRolePermissionsByRoleIdsAsync(new List<int>{ role.Id }, cancellationToken).ConfigureAwait(false);
        
        var permissions = await _securityDataProvider.GetPermissionsByIdsAsync(
            rolePermissions.Select(x => x.PermissionId).ToList(), cancellationToken);
        
        var mapperRolePermissions = _mapper.Map<List<RolePermissionDto>>(rolePermissions);

        foreach (var rolePermission in mapperRolePermissions)
        {
            rolePermission.RoleName = role.Name;
            rolePermission.PermissionName = permissions.FirstOrDefault(x => x.Id == rolePermission.PermissionId)?.Name;
        }
        
        var roleUsers = await _securityDataProvider.GetRoleUsersAsync(roleId: role.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var rolePermissionUsers = await _securityDataProvider.GetRolePermissionUsersAsync(roleId: role.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new GetPermissionsByRoleIdResponse
        {
            Data = new GetPermissionsByRoleIdResponseData
            {
                Role = _mapper.Map<RoleDto>(role),
                RoleUsers = roleUsers,
                Permissions = _mapper.Map<List<PermissionDto>>(permissions),
                RolePermissions = mapperRolePermissions,
                RolePermissionUsers = _mapper.Map<List<RolePermissionUserDto>>(rolePermissionUsers)
            }
        };
    }
}