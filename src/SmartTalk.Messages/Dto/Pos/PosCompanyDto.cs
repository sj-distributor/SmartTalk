namespace SmartTalk.Messages.Dto.Pos;

public class PosCompanyDto
{
    public int Id { get; set; }
    
    public int? ServiceProviderId { get; set; }
    
    public string Name { get; set; }
    
    public string Description { get; set; }
    
    public bool Status { get; set; }
    
    public int? CreatedBy { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public int? LastModifiedBy { get; set; }
    
    public DateTimeOffset? LastModifiedDate { get; set; }
}