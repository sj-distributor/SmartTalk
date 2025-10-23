using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class DeleteAutoTestTaskCommand : ICommand
{ 
    public int TaskId { get; set; }
}

public class DeleteAutoTestTaskResponse : SmartTalkResponse
{
}