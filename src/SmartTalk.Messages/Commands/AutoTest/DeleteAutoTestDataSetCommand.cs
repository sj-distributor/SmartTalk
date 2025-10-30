using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class DeleteAutoTestDataSetCommand : ICommand
{
    public int AutoTestDataSetId { get; set; }
}

public class DeleteAutoTestDataSetResponse : SmartTalkResponse
{
}