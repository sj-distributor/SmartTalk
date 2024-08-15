using SmartTalk.Messages.Dto.Security;
using SmartTalk.Messages.DTO.Security;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.Messages.Dto.Account;

public class UserAccountDto
{
    public int Id { get; set; }
    
    public DateTimeOffset CreatedOn { get; set; }
    
    public DateTimeOffset ModifiedOn { get; set; }
    
    public Guid Uuid { get; set; }
    
    public string UserName { get; set; }
    
    public bool IsActive { get; set; }
    
    public string ThirdPartyUserId { get; set; }
    
    public UserAccountIssuer Issuer { get; set; }
    
    public List<RoleDto> Roles { get; set; } = new();

    public List<PermissionDto> Permissions { get; set; } = new();
    
    public UserAccountProfileDto UserAccountProfile { get; set; }
}