using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class UpdateAutoTestTestTaskCommand : ICommand
{
    public int TestTaskId { get; set; }
    
    public AutoTestTestTaskStatus Status { get; set; }
}
 
public class UpdateAutoTestTestTaskResponse : SmartTalkResponse<AutoTestTestTaskDto>
{
}