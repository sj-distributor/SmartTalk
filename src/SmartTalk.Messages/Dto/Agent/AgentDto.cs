using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Messages.Dto.Agent;

public class AgentDto
{
    public int Id { get; set; }

    public int? RelateId { get; set; }
    
    public int? ServiceProviderId { get; set; }
    
    public bool IsDisplay { get; set; }
    
    public string WechatRobotKey { get; set; }
    
    public string WechatRobotMessage { get; set; }
    
    public AgentType Type { get; set; }
    
    public AgentSourceSystem SourceSystem { get; set; }
    
    public bool IsWecomMessageOrder { get; set; } = false;
    
    public bool IsSendAnalysisReportToWechat { get; set; } = false;
    
    public bool IsSendAudioRecordWechat { get; set; } = false;
    
    public string Timezone { get; set; }
    
    public string Name { get; set; }
    
    public string Brief { get; set; }
    
    public AiSpeechAssistantChannel? Channel { get; set; }
    
    public bool IsReceiveCall { get; set; }
    
    public bool IsSurface { get; set; }
    
    public string Voice { get; set; }
    
    public int WaitInterval { get; set; }
    
    public bool IsTransferHuman { get; set; }
    
    public string TransferCallNumber { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }

    public int UnreviewCount { get; set; } = 0;
    
    public List<AiSpeechAssistantDto> Assistants { get; set; }
}