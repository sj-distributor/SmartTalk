using Mediator.Net.Contracts;
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

public class GetPhoneOrderRecordTasksResponse : SmartTalkResponse<List<GetPhoneOrderRecordTasksResponseData>>
{
}

public class GetPhoneOrderRecordTasksResponseData
{
    public int StortId { get; set; }
    
    public int AgentId { get; set; }

    public int RecordId { get; set; }

    public DialogueScenarios? Scenarios { get; set; }
    
    public DateTimeOffset RecordDate { get; set; }

    public string TaskSource { get; set; }
}
