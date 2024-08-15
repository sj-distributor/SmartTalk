namespace SmartTalk.Messages.DTO.Security.Data;

public class CreateOrUpdateRoleUserDto
{
    public int Id { get; set; }
    
    public int RoleId { get; set; }

    public int UserId { get; set; }
}