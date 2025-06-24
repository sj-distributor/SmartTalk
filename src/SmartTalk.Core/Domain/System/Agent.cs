using SmartTalk.Messages.Dto.Agent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Agent;

namespace SmartTalk.Core.Domain.System;

[Table("agent")]
public class Agent : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("wechat_robot_key"), StringLength(256)]
    public string WechatRobotKey { get; set; }
    
    [Column("wechat_robot_message")]
    public string WechatRobotMessage { get; set; }

    [Column("relate_id")]
    public int RelateId { get; set; }

    [Column("type")]
    public AgentType Type { get; set; }
    
    [Column("source_system")]
    public AgentSourceSystem SourceSystem { get; set; }
    
    [Column("is_wecom_message_order")]
    public bool IsWecomMessageOrder { get; set; } = false;
    
    [Column("is_send_analysis_report_to_wechat")]
    public bool IsSendAnalysisReportToWechat { get; set; } = false;
    
    [Column("file_type")]
    public AgentFileType FileType { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}