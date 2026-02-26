using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderRecordTasksRequest : HasServiceProviderId, IRequest
{
    public List<int> AgentIds { get; set; }
    
    public DateTimeOffset? Date { get; set; }

    public WaitingTaskStatus? WaitingTaskStatus { get; set; }

    public List<TaskType> TaskType { get; set; }
}

public class GetPhoneOrderRecordTasksResponse : SmartTalkResponse<GetPhoneOrderRecordTasksDto>
{
}

public class GetPhoneOrderRecordTasksDto
{
    public int AllCount { get; set; }

    public int UnreadCount { get; set; }
    
    public List<WaitingProcessingEventsDto> WaitingTasks { get; set; }
}