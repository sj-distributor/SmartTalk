namespace SmartTalk.Messages.DTO.Security;

public class RolePermissionUserDto
{
    public int Id { get; set; }
    
    public int RoleId { get; set; }
    
    public int PermissionId { get; set; }
    
    public List<int> UserIds { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public DateTimeOffset ModifiedDate { get; set; }
}