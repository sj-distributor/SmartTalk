using SmartTalk.Messages.Enums.Security;

namespace SmartTalk.Messages.Dto.Security.Data;

public class PermissionRatingLevelDto
{
    public int Id { get; set; }
    
    public int PermissionId { get; set; }
    
    public PermissionLevel PermissionLevel { get; set; }
}