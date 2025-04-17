namespace SmartTalk.Messages.Dto.PosManagement;

public class PosCompanyDto
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public string Description { get; set; }
    
    public string Address { get; set; }
    
    public bool Status { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}