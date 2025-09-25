using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.HrInterView;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.HrInterView;

public class GetHrInterViewSettingsRequest : IRequest
{
}

public class GetHrInterViewSettingsResponse : SmartTalkResponse<List<HrInterViewSettingDto>>
{
}