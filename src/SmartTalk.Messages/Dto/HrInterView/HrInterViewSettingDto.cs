using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Messages.Dto.HrInterView;

public class HrInterViewSettingDto
{
    public int? Id { get; set; }
    
    public string Welcome { get; set; }
    
    public string EndMessage { get; set; }
    
    public Guid SessionId { get; set; }
    
    public bool IsConvertText { get; set; } = false;
    
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
    
    [NotMapped]
    public List<HrInterViewSettingQuestionDto> Questions { get; set; }
}