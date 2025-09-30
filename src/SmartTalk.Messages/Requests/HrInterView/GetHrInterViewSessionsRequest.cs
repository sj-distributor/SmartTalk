using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.HrInterView;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.HrInterView;

public class GetHrInterViewSessionsRequest : IRequest
{
    public Guid? SettingId { get; set; }

    public int? PageIndex { get; set; }

    public int? PageSzie { get; set; }
}

public class GetHrInterViewSessionsResponse : SmartTalkResponse
{
    public List<HrInterViewSessionGroupDto> SessionGroups { get; set; }
    
    public int TotalCount { get; set; }
}

public class HrInterViewSessionGroupDto
{
    public Guid SessionId { get; set; }

    public List<HrInterViewSessionDto> Sessions { get; set; }
}