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
    
    public DateTimeOffset? Date { get; set; }
    
    public List<string> OrderIds { get; set; }
}

public class GetPhoneOrderRecordsResponse : SmartTalkResponse<List<PhoneOrderRecordDto>>
{
}