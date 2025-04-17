namespace SmartTalk.Messages.Dto.PosManagement;

public class PosCompanyStoreDto
{
    public int Id { get; set; }
    
    public int CompanyId { get; set; }
    
    public string EnName { get; set; }
    
    public string ZhName { get; set; }
    
    public string Description { get; set; }
    
    public bool Status { get; set; }
    
    public string PhoneNums { get; set; }
    
    public string Logo { get; set; }
    
    public string Address { get; set; }
    
    public string Latitude { get; set; }
    
    public string Longitude { get; set; }
    
    public string PosUrl { get; set; }
    
    public string AppleId { get; set; }
    
    public string AppSecret { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}