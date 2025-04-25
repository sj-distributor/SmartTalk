using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.VoiceAi.PosManagement;

public class GetPosCompanyDetailRequest : IRequest
{
    public int Id { get; set; }
}

public class GetPosCompanyDetailResponse : SmartTalkResponse<PosCompanyDto>
{
}