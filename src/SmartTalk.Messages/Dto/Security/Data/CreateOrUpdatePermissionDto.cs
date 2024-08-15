namespace SmartTalk.Messages.DTO.Security.Data;

public class CreateOrUpdatePermissionDto
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public string Description { get; set; }

    public bool IsSystem { get; set; }
}