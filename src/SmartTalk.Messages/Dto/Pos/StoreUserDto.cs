namespace SmartTalk.Messages.Dto.Pos;

public class StoreUserDto
{
    public int Id { get; set; }
    
    public int UserId { get; set; }
    
    public string UserName { get; set; }
    
    public int StoreId { get; set; }
    
    public int? CreatedBy { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public int? LastModifiedBy { get; set; }
    
    public DateTimeOffset? LastModifiedDate { get; set; }
}