using SmartTalk.Messages.Enums.Agent;

namespace SmartTalk.Messages.Dto.Agent;

public class AgentDto
{
    public int Id { get; set; }

    public int? RelateId { get; set; }
    
    public int? PosServiceId { get; set; }
    
    public bool IsDisplay { get; set; }
    
    public string WechatRobotKey { get; set; }
    
    public string WechatRobotMessage { get; set; }
    
    public AgentType Type { get; set; }
    
    public AgentSourceSystem SourceSystem { get; set; }
    
    public bool IsWecomMessageOrder { get; set; } = false;
    
    public bool IsSendAnalysisReportToWechat { get; set; } = false;
    
    public bool IsSendAudioRecordWechat { get; set; } = false;
    
    public string Timezone { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}