using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class CreateAutoTestTestTaskCommand : ICommand
{
    public AutoTestTestTaskDto TestTask { get; set; }
}

public class CreateAutoTestTestTaskResponse : SmartTalkResponse<AutoTestTestTaskDto>
{
}