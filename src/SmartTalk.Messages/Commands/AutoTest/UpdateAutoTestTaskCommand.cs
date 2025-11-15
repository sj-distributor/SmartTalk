using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class UpdateAutoTestTaskCommand : ICommand
{
    public int TaskId { get; set; }
    
    public AutoTestTaskStatus Status { get; set; }
}
 
public class UpdateAutoTestTaskResponse : SmartTalkResponse<AutoTestTaskDto>
{
}