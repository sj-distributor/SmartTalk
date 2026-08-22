using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Messages.Commands.RealtimeAiWebRtc;

public class CreateRealtimeAiWebRtcSessionCommand : ICommand
{
    public int AssistantId { get; set; }

    public RealtimeAiServerRegion Region { get; set; }

    public string OfferSdp { get; set; }
}

public class CreateRealtimeAiWebRtcSessionResponse : IResponse
{
    public string CallId { get; set; }

    public string AnswerSdp { get; set; }
}
