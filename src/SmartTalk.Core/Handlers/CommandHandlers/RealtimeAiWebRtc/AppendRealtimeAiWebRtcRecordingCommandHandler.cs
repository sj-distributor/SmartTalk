using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
using SmartTalk.Messages.Commands.RealtimeAiWebRtc;

namespace SmartTalk.Core.Handlers.CommandHandlers.RealtimeAiWebRtc;

public class AppendRealtimeAiWebRtcRecordingCommandHandler
    : ICommandHandler<AppendRealtimeAiWebRtcRecordingCommand, AppendRealtimeAiWebRtcRecordingResponse>
{
    private readonly IRealtimeAiWebRtcSessionRegistry _registry;

    public AppendRealtimeAiWebRtcRecordingCommandHandler(IRealtimeAiWebRtcSessionRegistry registry)
    {
        _registry = registry;
    }

    public async Task<AppendRealtimeAiWebRtcRecordingResponse> Handle(
        IReceiveContext<AppendRealtimeAiWebRtcRecordingCommand> context,
        CancellationToken cancellationToken)
    {
        var command = context.Message;
        var result = await _registry
            .AppendRecordingAsync(command.CallId, command.Sequence, command.PcmBytes, command.IsFinal)
            .ConfigureAwait(false);

        return new AppendRealtimeAiWebRtcRecordingResponse
        {
            Status = result.Status,
            NextSequence = result.NextSequence
        };
    }
}
