namespace SmartTalk.Messages.Dto.Security;

public class UserPermissionDto
{
    public int Id { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public DateTimeOffset LastModifiedDate { get; set; }
    
    public int UserId { get; set; }

    public int PermissionId { get; set; }
    
    public string UserName { get; set; }

    public string PermissionName { get; set; }
}