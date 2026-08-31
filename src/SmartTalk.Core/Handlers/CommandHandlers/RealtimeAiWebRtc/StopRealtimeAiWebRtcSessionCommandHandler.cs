using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
using SmartTalk.Messages.Commands.RealtimeAiWebRtc;

namespace SmartTalk.Core.Handlers.CommandHandlers.RealtimeAiWebRtc;

public class StopRealtimeAiWebRtcSessionCommandHandler
    : ICommandHandler<StopRealtimeAiWebRtcSessionCommand, StopRealtimeAiWebRtcSessionResponse>
{
    private readonly IRealtimeAiWebRtcSessionRegistry _registry;

    public StopRealtimeAiWebRtcSessionCommandHandler(IRealtimeAiWebRtcSessionRegistry registry)
    {
        _registry = registry;
    }

    public async Task<StopRealtimeAiWebRtcSessionResponse> Handle(
        IReceiveContext<StopRealtimeAiWebRtcSessionCommand> context,
        CancellationToken cancellationToken)
    {
        var isFound = await _registry.StopAsync(context.Message.CallId).ConfigureAwait(false);

        return new StopRealtimeAiWebRtcSessionResponse { IsFound = isFound };
    }
}
