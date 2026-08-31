using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.RealtimeAiWebRtc;

public class AppendRealtimeAiWebRtcRecordingCommand : ICommand
{
    public string CallId { get; set; }

    public long Sequence { get; set; }

    public bool IsFinal { get; set; }

    public byte[] PcmBytes { get; set; }
}

public class AppendRealtimeAiWebRtcRecordingResponse : IResponse
{
    public RealtimeAiWebRtcRecordingAppendStatus Status { get; set; }

    public long NextSequence { get; set; }
}

public enum RealtimeAiWebRtcRecordingAppendStatus
{
    Accepted,
    Duplicate,
    NotFound,
    InvalidSequence,
    RecordingLimitExceeded,
    RateLimitExceeded,
    Finalized
}
