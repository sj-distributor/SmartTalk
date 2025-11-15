using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class CreateAutoTestTaskCommand : ICommand
{
    public AutoTestTaskDto Task { get; set; }
}

public class CreateAutoTestTaskResponse : SmartTalkResponse<AutoTestTaskDto>
{
}