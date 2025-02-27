using System.Net;
using AutoMapper;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Core.Middlewares.Security;
using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Dto.Security;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Messages.Requests.Security;
using SmartTalk.Messages.Commands.Security;
using SmartTalk.Messages.Enums.Security;
using SmartTalk.Messages.Events.Security;

namespace SmartTalk.Core.Services.Security;

public interface ISecurityService : IScopedDependency
{
    Task<UpdateUserAccountResponse> UpdateRoleUserAsync(UpdateUserAccountCommand command, CancellationToken cancellationToken);
    
     Task<GetCurrentUserRolesResponse> GetCurrentUserRoleAsync(
        GetCurrentUserRolesRequest request, CancellationToken cancellationToken);
     
     Task<GetPermissionsByRoleIdResponse> GetPermissionsByRoleIdAsync(
        GetPermissionsByRoleIdRequest request, CancellationToken cancellationToken);
     
     Task<GetRolesResponse> GetRolesAsync(GetRolesRequest request, CancellationToken cancellationToken);
     
     Task<UserPermissionsCreatedEvent> CreateUserPermissionsAsync(
         CreateUserPermissionsCommand command, CancellationToken cancellationToken);
}

public class SecurityService : ISecurityService
{
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IAccountDataProvider _accountDataProvider;
    private readonly ISecurityDataProvider _securityDataProvider;
    
    public SecurityService(IMapper mapper, ICurrentUser currentUser, IAccountDataProvider accountDataProvider, ISecurityDataProvider securityDataProvider)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _accountDataProvider = accountDataProvider;
        _securityDataProvider = securityDataProvider;
    }
                            
    public async Task<UpdateUserAccountResponse> UpdateRoleUserAsync(UpdateUserAccountCommand command, CancellationToken cancellationToken)
    {
        var roleUser = await _securityDataProvider.GetRoleUserByIdAsync(command.OldRoleId, command.UserId, cancellationToken).ConfigureAwait(false);
        
        roleUser.RoleId = command.NewRoleId;
        
        await _securityDataProvider.UpdateRoleUsersAsync([roleUser], cancellationToken).ConfigureAwait(false);

        return new UpdateUserAccountResponse
        {
            Data = _mapper.Map<UpdateUserAccountDto>(roleUser)
        };
    }
     
    public async Task<GetCurrentUserRolesResponse> GetCurrentUserRoleAsync(
        GetCurrentUserRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await _accountDataProvider.GetUserAccountDtoAsync(_currentUser.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (user.Item1 == 0) throw new AccountExpiredException("User Account Is Not Exist");

        var currentRoles = await _securityDataProvider.GetCurrentUserRolesAsync(request.SystemSource, cancellationToken).ConfigureAwait(false);

        if (!currentRoles.Any()) return new GetCurrentUserRolesResponse { Data = new GetCurrentUserRolesResponseData { UserAccount = user.Item2.FirstOrDefault() } };

        var rolePermissionData = await GetRolePermissionDataAsync(currentRoles, cancellationToken).ConfigureAwait(false);
        
        return new GetCurrentUserRolesResponse
        {
            Data = new GetCurrentUserRolesResponseData
            {
                Count = currentRoles.Count,
                RolePermissionData = rolePermissionData,
                UserAccount = user.Item2.FirstOrDefault()
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

    public async Task<GetRolesResponse> GetRolesAsync(GetRolesRequest request, CancellationToken cancellationToken)
    {
        var (count, roles) = await _securityDataProvider.GetRolesAsync(pageSize: request.PageSize, pageIndex: request.PageIndex, systemSource: RoleSystemSource.System, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GetRolesResponse
        {
            Data = new GetRolesResponseData
            {
                Count = count,
                Roles = _mapper.Map<List<RoleDto>>(roles)
            }
        };
    }
      
    public async Task<UserPermissionsCreatedEvent> CreateUserPermissionsAsync(CreateUserPermissionsCommand command, CancellationToken cancellationToken)
    {
        var userPermissions = _mapper.Map<List<UserPermission>>(command.UserPermissions);
        
        await _securityDataProvider.CreateUserPermissionsAsync(userPermissions, cancellationToken).ConfigureAwait(false);

        return new UserPermissionsCreatedEvent
        {
            UserPermissions = _mapper.Map<List<UserPermissionDto>>(userPermissions)
        };
    }
}