using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class UpdatePhoneOrderRecordTasksCommand : ICommand
{
    public List<int> Ids { get; set; }

    public WaitingTaskStatus WaitingTaskStatus { get; set; }
}

public class UpdatePhoneOrderRecordTasksResponse : SmartTalkResponse<List<WaitingProcessingEventsDto>>
{
}