using System.Net.WebSockets;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.HrInterView;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.HrInterView;

public class AddOrUpdateHrInterViewSettingCommand : ICommand
{
    public HrInterViewSettingDto Setting { get; set; }
    
    public List<HrInterViewSettingQuestionDto> Questions { get; set; }
}

public class AddOrUpdateHrInterViewSettingResponse : SmartTalkResponse
{
    public Guid SessionId { get; set; }
    
    public string StartMessage { get; set; }
    
    public string StartMessageFileUrl { get; set; }
    
    public string FirstQuestionMessage { get; set; }
    
    public string FirstQuestionFileUrl { get; set; }
    
    public string EndMessage { get; set; }
    
    public string EndMessageFileUrl { get; set; }
}