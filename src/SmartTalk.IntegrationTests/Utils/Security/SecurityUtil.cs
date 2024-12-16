using Autofac;
using Mediator.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Messages.Commands.Security;
using SmartTalk.Messages.Dto.Security.Data;
using SmartTalk.Messages.DTO.Security.Data;
using SmartTalk.Messages.Enums.Security;
using SmartTalk.Messages.Requests.Security;

namespace SmartTalk.IntegrationTests.Utils.Security;

public class SecurityUtil : TestUtil
{
    public SecurityUtil(ILifetimeScope scope) : base(scope)
    {
    }

    public async Task<int> AddRolesAsync(string? name = null, string? description = null)
    {
        name ??= "默认角色";
        
        var role = new Role
        {
            Name = name,
            IsSystem = true,
            Description = description ?? "默认描述",
            SystemSource = RoleSystemSource.System
        };
       
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAsync(role).ConfigureAwait(false);
        });
        
        return role.Id;
    }
    
    public async Task DeleteRolesAsync(int id)
    {
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync<DeleteRolesCommand, DeleteRolesResponse>(new DeleteRolesCommand
            {
                RoleIds = new List<int> { id }
            }).ConfigureAwait(false);
        });
    }
    
    public async Task UpdateRolesAsync(int id, string name)
    {
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync<UpdateRolesCommand, UpdateRolesResponse>(new UpdateRolesCommand
            {
               Roles = new List<CreateOrUpdateRoleDto>
               {
                   new ()
                   {
                       Id = id,
                       Name = name
                   }
               }
            }).ConfigureAwait(false);
        });
    }
    
    public async Task<GetRolesResponse> GetRolesAsync(int? pageIndex = null, int? pageSize = null, string? keyword = null)
    {
       return await RunWithUnitOfWork<IMediator, GetRolesResponse>(
           async mediator => await mediator.RequestAsync<GetRolesRequest, GetRolesResponse>(new GetRolesRequest
       {
           PageIndex = pageIndex ?? 1,
           PageSize = pageSize ?? 10,
           Keyword = keyword
       })).ConfigureAwait(false);
    }
    
    public async Task<int> AddRoleUsersAsync(int userId, int roleId)
    {
        var roleUserId = 0;
        var roleUser = new CreateOrUpdateRoleUserDto
        {
            UserId = userId,
            RoleId = roleId,
        };
        
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync<CreateRoleUsersCommand, CreateRoleUsersResponse>(new CreateRoleUsersCommand
            {
                RoleUsers = new List<CreateOrUpdateRoleUserDto> { roleUser }
            }).ConfigureAwait(false);
        });
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            roleUserId = (await repository.QueryNoTracking<RoleUser>()
                .Where(x => x.UserId == userId && x.RoleId == roleId).ToListAsync().ConfigureAwait(false)).First().Id;
        });
        
        return roleUserId;
    }
    
    public async Task DeleteRoleUsersAsync(int id)
    {
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync<DeleteRoleUsersCommand, DeleteRoleUsersResponse>(new DeleteRoleUsersCommand
            {
                RoleUserIds = new List<int> { id }
            }).ConfigureAwait(false);
        });
    }
    
    public async Task UpdateRoleUsersAsync(int id, int roleId, int userId)
    {
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync<UpdateRoleUsersCommand, UpdateRoleUsersResponse>(new UpdateRoleUsersCommand
            {
                RoleUsers = new List<CreateOrUpdateRoleUserDto>
                {
                    new ()
                    {
                        Id = id,
                        RoleId = roleId,
                        UserId = userId,
                    }
                }
            }).ConfigureAwait(false);
        });
    }
    
    public async Task<GetRoleUsersResponse> GetRoleUsersAsync(int? pageIndex = null, int? pageSize = null, string? keyword = null)
    {
        return await RunWithUnitOfWork<IMediator, GetRoleUsersResponse>(
            async mediator => await mediator.RequestAsync<GetRoleUsersRequest, GetRoleUsersResponse>(new GetRoleUsersRequest
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                Keyword = keyword
            })).ConfigureAwait(false);
    }
    
    public async Task<int> AddRolePermissionsAsync(int permissionId, int roleId)
    {
        var rolePermissionId = 0;
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAsync(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });
        }).ConfigureAwait(false);

        await RunWithUnitOfWork<IRepository>(async repository =>
        {
           rolePermissionId = (await repository.QueryNoTracking<RolePermission>()
               .Where(x => x.PermissionId == permissionId && x.RoleId == roleId).ToListAsync().ConfigureAwait(false)).First().Id;
        }).ConfigureAwait(false);

        return rolePermissionId;
    }
    
    public async Task<GetRolePermissionsResponse> GetRolePermissionsAsync(int? pageIndex = null, int? pageSize = null, string? keyword = null)
    {
        return await RunWithUnitOfWork<IMediator, GetRolePermissionsResponse>(
            async mediator => await mediator.RequestAsync<GetRolePermissionsRequest, GetRolePermissionsResponse>(new GetRolePermissionsRequest
            {
                PageIndex = pageIndex ?? 1,
                PageSize = pageSize ?? 10,
                Keyword = keyword
            })).ConfigureAwait(false);
    }
    
    public async Task<int> AddUserPermissionsAsync(int permissionId, int userId)
    {
        var userPermissionId = 0;
        
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync<CreateUserPermissionsCommand, CreateUserPermissionsResponse>(new CreateUserPermissionsCommand
            {
                UserPermissions = new List<CreateOrUpdateUserPermissionDto>
                {
                    new ()
                    {
                        UserId = userId,
                        PermissionId = permissionId,
                    }
                }
            }).ConfigureAwait(false);
        });
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            userPermissionId = (await repository.QueryNoTracking<UserPermission>().Where(x => x.PermissionId == permissionId && x.UserId == userId).ToListAsync()
                .ConfigureAwait(false)).First().Id;
        });
        
        return userPermissionId;
    }
    
    public async Task DeleteUserPermissionsAsync(int id)
    {
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync<DeleteUserPermissionsCommand, DeleteUserPermissionsResponse>(new DeleteUserPermissionsCommand
            {
                UserPermissionIds = new List<int> { id }
            }).ConfigureAwait(false);
        });
    }
    
    public async Task UpdateUserPermissionsAsync(int id, int userId, int permissionId)
    {
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync<UpdateUserPermissionsCommand, UpdateUserPermissionsResponse>(new UpdateUserPermissionsCommand
            {
                UserPermissions = new List<CreateOrUpdateUserPermissionDto>
                {
                    new ()
                    {
                        Id = id,
                        UserId = userId,
                        PermissionId = permissionId,
                    }
                }
            }).ConfigureAwait(false);
        });
    }
    
    public async Task<GetUserPermissionsResponse> GetUserPermissionsAsync(int? pageIndex = null, int? pageSize = null, string? keyword = null)
    {
        return await RunWithUnitOfWork<IMediator, GetUserPermissionsResponse>(
            async mediator => await mediator.RequestAsync<GetUserPermissionsRequest, GetUserPermissionsResponse>(new GetUserPermissionsRequest
            {
                PageIndex = pageIndex ?? 1,
                PageSize = pageSize ?? 10,
                Keyword = keyword
            })).ConfigureAwait(false);
    }
    
    public async Task<int> AddPermissionsAsync(string? name = null, List<Permission>? permissions = null)
    {
        var permissionId = 0;
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAllAsync(permissions ?? new List<Permission>
            {
                new() { Name = name }
            });
        });
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            permissionId = (await repository.QueryNoTracking<Permission>().Where(x => x.Name == name).ToListAsync()
                .ConfigureAwait(false)).First().Id;
        });

         return permissionId;
    }
    
    public async Task AddPermissionsAndAssignToUserAsync(int userId, List<string> permissionNames)
    {
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            foreach (var newPermission in permissionNames.Select(permissionName => new Permission
                     {
                         Name = permissionName,
                         IsSystem = true
                     }))
            {
                if ((await repository.QueryNoTracking<Permission>(x => x.Name == newPermission.Name)
                        .ToListAsync().ConfigureAwait(false)).IsNullOrEmpty())
                    await repository.InsertAsync(newPermission);
            }
        });

        var permissionIds = new List<int>();

        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            permissionIds = (await repository.Query<Permission>().ToListAsync().ConfigureAwait(false)).Select(x => x.Id).ToList();
        });

        await AssignPermissionsToUserAsync(permissionIds, userId);
    }

    private async Task AssignPermissionsToUserAsync(IEnumerable<int> permissionIds, int userId)
    {
        var userPermissions = permissionIds.Select(permissionId =>
            new CreateOrUpdateUserPermissionDto
            {
                UserId = userId,
                PermissionId = permissionId
            }).ToList();

        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync<CreateUserPermissionsCommand, CreateUserPermissionsResponse>(new CreateUserPermissionsCommand
            {
                UserPermissions = userPermissions
            }).ConfigureAwait(false);
        });
    }
    
    public async Task AddPermissionsAsync(int permissionId, string name, string permissionDescription)
    {
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAsync(new Permission
            {
                Id = permissionId,
                Name = name,
                Description = permissionDescription
            }).ConfigureAwait(false);
        });
    }
    
    public async Task<GetPermissionsResponse> GetPermissionsAsync(int? pageIndex = null, int? pageSize = null)
    {
        return await RunWithUnitOfWork<IMediator, GetPermissionsResponse>(
            async mediator => await mediator.RequestAsync<GetPermissionsRequest, GetPermissionsResponse>(new GetPermissionsRequest
            {
                PageIndex = pageIndex ?? 1,
                PageSize = pageSize ?? 10
            })).ConfigureAwait(false);
    }
    
    public async Task GrantPermissionsIntoRoleAsync(
        List<CreateOrUpdateRolePermissionDto> rolePermissions, int roleId, string roleName, List<CreateOrUpdateRoleUserDto>? roleUsers = null)
    {
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync<GrantPermissionsIntoRoleCommand, GrantPermissionsIntoRoleResponse>(new GrantPermissionsIntoRoleCommand
            {
                Role = new CreateOrUpdateRoleDto
                {
                    Id = roleId,
                    Name = roleName,
                    Description = "默认角色描述"
                },
                RoleUsers = roleUsers,
                RolePermissions = rolePermissions
            }).ConfigureAwait(false);
        });
    }
    
    public async Task UpdateGrantPermissionsIntoRoleAsync(
        List<CreateOrUpdateRolePermissionDto> rolePermissions, int roleId, string roleName, List<CreateOrUpdateRoleUserDto>? roleUsers = null)
    {
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync<UpdatePermissionsOfRoleCommand, UpdatePermissionsOfRoleResponse>(new UpdatePermissionsOfRoleCommand
            {
                Role = new CreateOrUpdateRoleDto
                {
                    Id = roleId,
                    Name = roleName,
                    Description = "默认角色描述"
                },
                RoleUsers = roleUsers,
                RolePermissions = rolePermissions
            }).ConfigureAwait(false);
        });
    }
    
    public async Task<GetPermissionsByRoleIdResponse> GetGrantPermissionsIntoRolesAsync(int roleId)
    {
        return await RunWithUnitOfWork<IMediator, GetPermissionsByRoleIdResponse>(async mediator => 
                await mediator.RequestAsync<GetPermissionsByRoleIdRequest, GetPermissionsByRoleIdResponse>(new GetPermissionsByRoleIdRequest
        {
            RoleId = roleId
        }).ConfigureAwait(false));
    }
    
    public async Task<GetPermissionsByRolesResponse> GetPermissionsByRolesAsync(int? pageIndex = null, int? pageSize = null, string? keyword = null)
    {
        return await RunWithUnitOfWork<IMediator, GetPermissionsByRolesResponse>(async mediator => 
            await mediator.RequestAsync<GetPermissionsByRolesRequest, GetPermissionsByRolesResponse>(new GetPermissionsByRolesRequest
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                Keyword = keyword
            }).ConfigureAwait(false));
    }
    
    public async Task<GetCurrentUserRolesResponse> GetCurrentUserRolesAsync()
    {
        return await RunWithUnitOfWork<IMediator, GetCurrentUserRolesResponse>(async mediator => 
                await mediator.RequestAsync<GetCurrentUserRolesRequest, GetCurrentUserRolesResponse>(
                    new GetCurrentUserRolesRequest()).ConfigureAwait(false));
    }

    public async Task<int> AddRolePermissionUsersAsync(int roleId, int permissionId, List<Guid> userId)
    {
        var rolePermissionUserId = 0;
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAsync(new RolePermissionUser
            {
                RoleId = roleId,
                PermissionId = permissionId,
                UserIds = string.Join(',', userId)
            }).ConfigureAwait(false);
        });
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            rolePermissionUserId = (await repository.QueryNoTracking<RolePermissionUser>
                    (x => x.RoleId == roleId && x.PermissionId == permissionId).ToListAsync().ConfigureAwait(false)).First().Id;
        });

        return rolePermissionUserId;
    }
}