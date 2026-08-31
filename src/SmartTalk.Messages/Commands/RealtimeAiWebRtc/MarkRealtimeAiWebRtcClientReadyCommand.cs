using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.RealtimeAiWebRtc;

public class MarkRealtimeAiWebRtcClientReadyCommand : ICommand
{
    public string CallId { get; set; }
}

public class MarkRealtimeAiWebRtcClientReadyResponse : IResponse
{
    public bool IsFound { get; set; }
}
