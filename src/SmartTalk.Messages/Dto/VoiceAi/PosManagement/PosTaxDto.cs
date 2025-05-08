namespace SmartTalk.Messages.Dto.VoiceAi.PosManagement;

public class PosTaxDto
{
    public string Name { get; set; }
    
    public bool IsPercentage { get; set; }
    
    public bool IsSelectedByDefault { get; set; }
    
    public decimal Value { get; set; }
    
    public long Id { get; set; }
}