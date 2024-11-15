using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Dto.Security.Data;
using SmartTalk.Messages.DTO.Security.Data;
using SmartTalk.Messages.Enums.MessageLogging;

namespace SmartTalk.Messages.Commands.Security;

[SmartTalkLogging(LoggingSystemType.Security)]
public class GrantPermissionsIntoRoleCommand : ICommand
{
    public CreateOrUpdateRoleDto Role { get; set; }
    
    public List<CreateOrUpdateRoleUserDto> RoleUsers { get; set; }
    
    public List<CreateOrUpdateRolePermissionDto> RolePermissions { get; set; }
}

public class GrantPermissionsIntoRoleResponse : SmartTalkResponse<GrantPermissionsIntoRoleResponseData>
{
}

public class GrantPermissionsIntoRoleResponseData
{
    public RoleDto Role { get; set; }
    
    public List<RoleUserDto> RoleUsers { get; set; }
    
    public List<RolePermissionDto> RolePermissions { get; set; }
    
    public List<RolePermissionUserDto> RolePermissionUsers { get; set; }
}