using AutoMapper;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.Security;

namespace SmartTalk.Core.Services.Security;

public partial interface ISecurityDataProvider : IScopedDependency
{
    Task CreateRolesAsync(
        List<Role> roles, CancellationToken cancellationToken, bool forceSave = true);
    
    Task CreateRoleUsersAsync(
        List<RoleUser> roleUsers, CancellationToken cancellationToken, bool forceSave = true);
    
    Task CreateRolePermissionsAsync(
        List<RolePermission> rolePermissions, CancellationToken cancellationToken, bool forceSave = true);
    
    Task CreateUserPermissionsAsync(
        List<UserPermission> userPermissions, CancellationToken cancellationToken, bool forceSave = true);
    
    Task CreatePermissionsAsync(
        List<Permission> permissions, CancellationToken cancellationToken, bool forceSave = true);

    Task GrantPermissionsIntoRoleAsync(
        List<Permission> permissions, Role role, CancellationToken cancellationToken);
    
    Task CreateRolePermissionUsersAsync(
        List<RolePermissionUser> rolePermissionUsers, CancellationToken cancellationToken, bool forceSave = true);
    
    Task DeleteRolesAsync(List<Role> roles, CancellationToken cancellationToken);
    
    Task DeleteRolesRelatedAsync(List<Role> roles, CancellationToken cancellationToken);
    
    Task DeleteRoleUsersAsync(List<RoleUser> roleUsers, CancellationToken cancellationToken);
    
    Task DeleteRolePermissionsAsync(
        List<RolePermission> rolePermissions, CancellationToken cancellationToken);
    
    Task DeleteUserPermissionsAsync(
        List<UserPermission> userPermissions, CancellationToken cancellationToken);
    
    Task DeletePermissionsAsync(
        List<Permission> permissions, CancellationToken cancellationToken);
    
    Task DeletePermissionsRelatedAsync(
        List<Permission> permissions, CancellationToken cancellationToken);
    
    Task DeleteRolePermissionUsersAsync(
        List<RolePermissionUser> rolePermissionUnits, CancellationToken cancellationToken = default, bool forceSave = true);
    
    Task UpdateRolesAsync(
        List<Role> roles, CancellationToken cancellationToken, bool forceSave = true);
    
    Task UpdateRoleUsersAsync(
        List<RoleUser> roleUsers, CancellationToken cancellationToken, bool forceSave = true);
    
    Task UpdateRolePermissionsAsync(
        List<RolePermission> rolePermissions, CancellationToken cancellationToken, bool forceSave = true);

    Task UpdateUserPermissionsAsync(
        List<UserPermission> userPermissions, CancellationToken cancellationToken, bool forceSave = true);
    
    Task UpdatePermissionsAsync(
        List<Permission> permissions, CancellationToken cancellationToken, bool forceSave = true);
    
    Task UpdateRolePermissionUsersAsync(
        List<RolePermissionUser> rolePermissionUnits, CancellationToken cancellationToken, bool forceSave = true);
    
    Task<List<Role>> GetRolesByIdsAsync(
        List<int> roleIds, CancellationToken cancellationToken);
    
    Task<List<RoleUser>> GetRoleUsersByIdsAsync(
        List<int> roleUsersIds, CancellationToken cancellationToken);
    
    Task<List<RolePermission>> GetRolePermissionsByIdsAsync(
        List<int> rolePermissionsIds, CancellationToken cancellationToken);
    
    Task<List<RolePermission>> GetRolePermissionsByRoleIdsAsync(
        List<int> roleIds, CancellationToken cancellationToken);

    Task<List<UserPermission>> GetUserPermissionsByIdsAsync(
        List<int> userPermissionsIds, CancellationToken cancellationToken);
    
    Task<List<Permission>> GetPermissionsByIdsAsync(
        List<int> permissionsIds, CancellationToken cancellationToken);
    
    Task<List<Permission>> GetPermissionsOfRolesAsync(
        List<RoleDto> roles, CancellationToken cancellationToken);
    
    Task<(int, List<Role>)> GetRolesAsync(
        int? pageIndex = null, int? pageSize = null, string keyword = null, int? userId = null, RoleSystemSource? systemSource = null, UserAccountLevel? accountLevel = null, CancellationToken cancellationToken = default);
    
    Task<(int, List<RoleUser>)> GetRoleUsersPagingAsync(
        int? pageIndex = null, int? pageSize = null, int? roleId = null, string keyword = null, CancellationToken cancellationToken = default);
    
    Task<List<RoleUserDto>> GetRoleUsersAsync(int? roleId = null, string keyword = null, CancellationToken cancellationToken = default);
    
    Task<(int, List<RolePermission>)> GetRolePermissionsAsync(
        int pageIndex, int pageSize, string keyword = null, RoleSystemSource? systemSource = null, CancellationToken cancellationToken = default);
    
    Task<(int, List<UserPermission>)> GetUserPermissionsAsync(
        int pageIndex, int pageSize, string keyword, CancellationToken cancellationToken);
    
    Task<(int, List<Permission>)> GetPermissionsPagingAsync(
        List<string> names = null, string keyword = null, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
    
    Task<List<Permission>> GetPermissionsAsync(
        List<string> names = null, string keyword = null, CancellationToken cancellationToken = default);

    Task<Role> GetRoleByIdAsync(int id, CancellationToken cancellationToken);

    Task<List<Role>> GetRolesByNameAsync(string name, CancellationToken cancellationToken);
    
    Task<Permission> GetPermissionByIdAsync(int id, CancellationToken cancellationToken);
    
    Task<List<Role>> GetCurrentUserRolesAsync(RoleSystemSource? systemSource = null, CancellationToken cancellationToken = default);
    
    Task<List<RolePermission>> GetRolePermissionsByUserIdAsync(int userId, CancellationToken cancellationToken);
    
    Task<List<RolePermission>> GetRolePermissionsByUserNameAsync(
        string userName, CancellationToken cancellationToken = default);
    
    Task<(int, List<RolePermissionUser>)> GetRolePermissionUsersPagingAsync(
        int pageIndex, int pageSize, int? roleId = null, int? permissionId = null, CancellationToken cancellationToken = default);
    
    Task<List<RolePermissionUser>> GetRolePermissionUsersAsync(
        int? roleId = null, int? permissionId = null, List<int> permissionIds = null, List<int> roleIds = null, CancellationToken cancellationToken = default);
    
    Task<RoleUser> GetRoleUserByIdAsync(int roleId, int userId, CancellationToken cancellationToken);

    Task<List<RoleUser>> GetRoleUserByPermissionNameAsync(string permissionName, CancellationToken cancellationToken);
}

public partial class SecurityDataProvider : ISecurityDataProvider
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public SecurityDataProvider(
        IMapper mapper,
        IRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _mapper = mapper;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }
}