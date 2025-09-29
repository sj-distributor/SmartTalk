using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.HrInterView;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.HrInterView;

public class GetHrInterViewSessionsRequest : IRequest
{
    public Guid? SettingId { get; set; }

    public int? PageIndex { get; set; } = 1;

    public int? PageSzie { get; set; } = 15;
}

public class GetHrInterViewSessionsResponse : SmartTalkResponse
{
    public List<HrInterViewSessionDto> Sessions { get; set; }
    
    public int TotalCount { get; set; }
}