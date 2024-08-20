namespace SmartTalk.Messages.DTO.Security.Data;

public class CreateOrUpdateRolePermissionDto
{
    public int Id { get; set; }
    
    public int RoleId { get; set; }
    
    public int PermissionId { get; set; }

    public List<int> UserIds { get; set; } = new();
}