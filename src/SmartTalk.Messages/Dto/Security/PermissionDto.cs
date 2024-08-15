namespace SmartTalk.Messages.Dto.Security;

public class PermissionDto
{
    public int Id { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    public DateTimeOffset LastModifiedDate { get; set; }

    public string Name { get; set; }
    
    public string DisplayName { get; set; }
    
    public string Description { get; set; }
    
    public bool IsSystem { get; set; }
}