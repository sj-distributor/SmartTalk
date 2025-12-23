using SmartTalk.Messages.Dto.Agent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Domain.System;

[Table("agent")]
public class Agent : IAgent, IEntity<int>, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("service_provider_id")]
    public int? ServiceProviderId { get; set; }
    
    [Column("wechat_robot_key"), StringLength(256)]
    public string WechatRobotKey { get; set; }
    
    [Column("wechat_robot_message")]
    public string WechatRobotMessage { get; set; }

    [Column("relate_id")]
    public int? RelateId { get; set; }

    [Column("type")]
    public AgentType Type { get; set; }
    
    [Column("source_system")]
    public AgentSourceSystem SourceSystem { get; set; }
    
    [Column("is_display")]
    public bool IsDisplay { get; set; }
    
    [Column("is_wecom_message_order")]
    public bool IsWecomMessageOrder { get; set; } = false;
    
    [Column("is_send_analysis_report_to_wechat")]
    public bool IsSendAnalysisReportToWechat { get; set; } = false;
    
    [Column("is_send_audio_record_wechat")]
    public bool IsSendAudioRecordWechat { get; set; } = false;
    
    [Column("timezone")]
    public string Timezone { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
    
    [Column("name")]
    public string Name { get; set; }
    
    [Column("brief")]
    public string Brief { get; set; }
    
    [Column("channel")]
    public AiSpeechAssistantChannel? Channel { get; set; }
    
    [Column("is_receive_call")]
    public bool IsReceiveCall { get; set; }
    
    [Column("is_surface")]
    public bool IsSurface { get; set; }
    
    [Column("voice"), StringLength(64)]
    public string Voice { get; set; }
    
    [Column("wait_interval")]
    public int WaitInterval { get; set; } = 500;
    
    [Column("is_transfer_human")]
    public bool IsTransferHuman { get; set; } = false;
        
    [Column("transfer_call_number"), StringLength(128)]
    public string TransferCallNumber { get; set; }
    
    [Column("service_hours"), StringLength(1024)]
    public string ServiceHours { get; set; }
    
    [NotMapped]
    public List<AISpeechAssistant.AiSpeechAssistant> Assistants { get; set; }
}