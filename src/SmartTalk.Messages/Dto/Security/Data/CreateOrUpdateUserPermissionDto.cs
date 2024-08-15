namespace SmartTalk.Messages.DTO.Security.Data;

public class CreateOrUpdateUserPermissionDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int PermissionId { get; set; }
}