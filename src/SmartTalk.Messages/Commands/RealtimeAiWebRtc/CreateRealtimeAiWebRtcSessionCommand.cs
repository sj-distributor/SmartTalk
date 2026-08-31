using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Messages.Commands.RealtimeAiWebRtc;

public class CreateRealtimeAiWebRtcSessionCommand : ICommand
{
    public Guid SessionId { get; set; }

    public int AssistantId { get; set; }

    public RealtimeAiServerRegion Region { get; set; }

    public string OfferSdp { get; set; }
}

public class CreateRealtimeAiWebRtcSessionResponse : IResponse
{
    public bool IsSessionAlreadyBound { get; set; }

    public string CallId { get; set; }

    public string AnswerSdp { get; set; }
}
