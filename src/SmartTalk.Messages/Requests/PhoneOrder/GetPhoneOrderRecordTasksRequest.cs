using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderRecordTasksRequest : IRequest
{
    public List<int> AgentIds { get; set; }

    public DateTimeOffset StartTime { get; set; }
    
    public DateTimeOffset EntTime { get; set; }

    public PhoneOrderRecordTaskType DoListTpye { get; set; }
}

public class GetPhoneOrderRecordTasksResponse : SmartTalkResponse<GetPhoneOrderRecordTasksResponseData>
{
}

public class GetPhoneOrderRecordTasksResponseData
{
    public List<PhoneOrderRecordTaskDto> Tasks { get; set; }

    public int UnProcessCount { get; set; }
}
