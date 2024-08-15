using SmartTalk.Messages.Enums.Security;

namespace SmartTalk.Messages.Dto.Security.Data;

public class CreateOrUpdateRoleDto
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public string Description { get; set; }
    
    public RoleSystemSource SystemSource { get; set; }
}