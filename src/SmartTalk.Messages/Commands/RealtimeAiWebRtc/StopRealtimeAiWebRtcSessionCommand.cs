using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.RealtimeAiWebRtc;

public class StopRealtimeAiWebRtcSessionCommand : ICommand
{
    public string CallId { get; set; }
}

public class StopRealtimeAiWebRtcSessionResponse : IResponse
{
    public bool IsFound { get; set; }
}
