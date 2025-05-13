using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.VoiceAi.PosManagement;

public class GetPosProductRequest : IRequest
{
    public int Id { get; set; }
}

public class GetPosProductResponse : SmartTalkResponse<PosProductDto>
{
}