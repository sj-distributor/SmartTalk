namespace SmartTalk.Messages.Dto.Agent;

public class AgentDto
{
    public int Id { get; set; }

    public int RelateId { get; set; }
    
    public string WechatRobotKey { get; set; }
    
    public AgentType Type { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}