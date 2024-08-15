namespace SmartTalk.Messages.Dto.Account;

public class UserAccountProfileDto
{
    public int Id { get; set; }
    
    public int UserAccountId { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }

    public string DisplayName { get; set; }
    
    public string Phone { get; set; }
    
    public string Email { get; set; }
}