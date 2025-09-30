using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.HrInterView;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.HrInterView;

public class GetHrInterViewSettingsRequest : IRequest
{
    public int? SettingId { get; set; }

    public int? PageIndex { get; set; }

    public int? PageSzie { get; set; }
}

public class GetHrInterViewSettingsResponse : SmartTalkResponse
{
    public List<HrInterViewSettingDto> Settings { get; set; }
    
    public int TotalCount { get; set; }
}