using Smarties.Messages.DTO.SmartAssistant.Domain;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Dto.Pos;

public class CompanyStoreDto
{
    public int Id { get; set; }
    
    public int? ServiceProviderId { get; set; }
    
    public int CompanyId { get; set; }
    
    public string Names { get; set; }
    
    public string Description { get; set; }
    
    public string CompanyDescription { get; set; }
    
    public bool Status { get; set; }
    
    public string PhoneNums { get; set; }
    
    public string Logo { get; set; }
    
    public string Address { get; set; }
    
    public string Latitude { get; set; }
    
    public string Longitude { get; set; }
    
    public string Link { get; set; }
    
    public string AppId { get; set; }
    
    public string TimePeriod { get; set; }
    
    public string Timezone { get; set; }
    
    public int? CreatedBy { get; set; }
    
    public string PosName { get; set; }
    
    public string PosId { get; set; }
    
    public bool IsLink { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public int? LastModifiedBy { get; set; }
    
    public DateTimeOffset? LastModifiedDate { get; set; }

    public int Count { get; set; } = 0;
}