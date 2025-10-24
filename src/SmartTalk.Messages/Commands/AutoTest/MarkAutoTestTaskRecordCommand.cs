using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class MarkAutoTestTaskRecordCommand : ICommand
{
    public int RecordId { get; set; }
}

public class MarkAutoTestTaskRecordResponse : SmartTalkResponse<AutoTestTaskRecordDto>;