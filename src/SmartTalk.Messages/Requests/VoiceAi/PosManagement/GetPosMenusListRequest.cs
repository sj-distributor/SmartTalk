using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.VoiceAi.PosManagement;

public class GetPosMenusListRequest : IRequest
{
    public int StoreId { get; set; }
}

public class GetPosMenusListResponse : SmartTalkResponse<List<PosMenuDto>>
{
}