using SmartTalk.Messages.Dto.Security;

namespace SmartTalk.Messages.DTO.Security;

public class RolePermissionDataDto
{
    public RoleDto Role { get; set; }
    
    public List<PermissionDto> Permissions { get; set; }
}