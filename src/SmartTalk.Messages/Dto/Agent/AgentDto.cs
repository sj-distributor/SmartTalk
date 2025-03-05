namespace SmartTalk.Messages.Dto.Agent;

public class AgentDto
{
    public int Id { get; set; }

    public int RelateId { get; set; }
    
    public string WechatRobotUrl { get; set; }
    
    public string WechatRobotUploadUrl { get; set; }
    
    public AgentType Type { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}