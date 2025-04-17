namespace SmartTalk.Messages.Dto.PosManagement;

public class PosCompanyStoreUserDto
{
    public int Id { get; set; }
    
    public int UserId { get; set; }
    
    public int StoreId { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}