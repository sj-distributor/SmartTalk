namespace SmartTalk.Core.Domain.AISpeechAssistant;

public class AiSpeechAssistantDynamicConfigRelatingCompany : IEntity
{
    public int Id { get; set; }
    
    public int ConfigId { get; set; }
    
    public int CompanyId { get; set; }
    
    public string CompanyName { get; set; }
}