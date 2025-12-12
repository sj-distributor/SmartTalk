using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

[SmartTalkAuthorize(Permissions = new[] { SecurityStore.Permissions.CanViewPhoneOrder })]
public class GetPhoneOrderRecordsRequest : IRequest
{
    public int? AgentId { get; set; }
    
    public int? StoreId { get; set; }
    
    public string Name { get; set; }
    
    public List<DialogueScenarios>? DialogueScenarios { get; set; }
    
    public DateTimeOffset? Date { get; set; }
    
    public string OrderId { get; set; }
    
    public int? AssistantId { get; set; }

    public bool IsFilteringScenarios { get; set; } = false;
}

public class GetPhoneOrderRecordsResponse : SmartTalkResponse<List<PhoneOrderRecordDto>>
{
}
