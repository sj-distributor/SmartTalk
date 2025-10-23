using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class DeleteAutoTestTestTaskCommand : ICommand
{
 public int TestTaskId { get; set; }
}

public class DeleteAutoTestTestTaskResponse : SmartTalkResponse
{
}