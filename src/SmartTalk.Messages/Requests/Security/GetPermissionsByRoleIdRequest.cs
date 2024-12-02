using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Security;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Security;

public class GetPermissionsByRoleIdRequest : IRequest
{
    public int RoleId { get; set; }
}

public class GetPermissionsByRoleIdResponse : SmartTalkResponse<GetPermissionsByRoleIdResponseData>
{
}

public class GetPermissionsByRoleIdResponseData
{
    public RoleDto Role { get; set; }
    
    public List<RoleUserDto> RoleUsers { get; set; }
    
    public List<PermissionDto> Permissions { get; set; }
    
    public List<RolePermissionDto> RolePermissions { get; set; }
    
    public List<RolePermissionUserDto> RolePermissionUsers { get; set; }
}