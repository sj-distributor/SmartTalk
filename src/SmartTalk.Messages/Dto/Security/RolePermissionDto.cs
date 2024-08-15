namespace SmartTalk.Messages.DTO.Security;

public class RolePermissionDto
{
    public int Id { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public DateTimeOffset LastModifiedDate { get; set; }
    
    public int RoleId { get; set; }
    
    public int PermissionId { get; set; }
    
    public string PermissionName { get; set; }
    
    public string RoleName { get; set; }
    
    public string Description { get; set; }
}