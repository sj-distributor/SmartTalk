namespace SmartTalk.Messages.DTO.Security;

public class RoleUserDto
{
    public int Id { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }

    public DateTimeOffset ModifiedDate { get; set; }

    public int RoleId { get; set; }

    public int UserId { get; set; }
    
    public string RoleName { get; set; }
    
    public string UserName { get; set; }
    
    public string GroupName { get; set; }
    
    public string PositionName { get; set; }
}