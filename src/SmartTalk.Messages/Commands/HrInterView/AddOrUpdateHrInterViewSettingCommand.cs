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
}