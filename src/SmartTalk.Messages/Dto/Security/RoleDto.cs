using SmartTalk.Messages.Enums.Security;

namespace SmartTalk.Messages.DTO.Security;

public class RoleDto
{
    public RoleDto()
    {
        CreatedDate = DateTimeOffset.Now;
        ModifiedDate = DateTimeOffset.Now;
    }
    
    public int Id { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public DateTimeOffset ModifiedDate { get; set; }
    
    public string Name { get; set; }
    
    public string DisplayName { get; set; }
    
    public RoleSystemSource SystemSource { get; set; }
    
    public string Description { get; set; }
    
    public bool IsSystem { get; set; }
}