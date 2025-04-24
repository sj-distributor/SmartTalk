namespace SmartTalk.Messages.Dto.VoiceAi.PosManagement;

public class PosCompanyStoreDto
{
    public int Id { get; set; }
    
    public int CompanyId { get; set; }
    
    public string EnName { get; set; }
    
    public string ZhName { get; set; }
    
    public string Description { get; set; }
    
    public string CompanyDescription { get; set; }
    
    public bool Status { get; set; }
    
    public string PhoneNums { get; set; }
    
    public string Logo { get; set; }
    
    public string Address { get; set; }
    
    public string Latitude { get; set; }
    
    public string Longitude { get; set; }
    
    public string Link { get; set; }
    
    public string AppleId { get; set; }
    
    public string AppSecret { get; set; }
    
    public string PosDisPlay { get; set; }
    
    public string PosId { get; set; }
    
    public bool IsLink { get; set; }
    
    public int CreatedBy { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public int? LastModifiedBy { get; set; }
    
    public DateTimeOffset? LastModifiedDate { get; set; }
}