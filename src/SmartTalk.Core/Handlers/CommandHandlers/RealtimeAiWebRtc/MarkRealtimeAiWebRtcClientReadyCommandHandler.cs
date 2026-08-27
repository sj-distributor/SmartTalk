using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
using SmartTalk.Messages.Commands.RealtimeAiWebRtc;

namespace SmartTalk.Core.Handlers.CommandHandlers.RealtimeAiWebRtc;

public class MarkRealtimeAiWebRtcClientReadyCommandHandler
    : ICommandHandler<MarkRealtimeAiWebRtcClientReadyCommand, MarkRealtimeAiWebRtcClientReadyResponse>
{
    private readonly IRealtimeAiWebRtcSessionRegistry _registry;

    public MarkRealtimeAiWebRtcClientReadyCommandHandler(IRealtimeAiWebRtcSessionRegistry registry)
    {
        _registry = registry;
    }

    public async Task<MarkRealtimeAiWebRtcClientReadyResponse> Handle(
        IReceiveContext<MarkRealtimeAiWebRtcClientReadyCommand> context,
        CancellationToken cancellationToken)
    {
        var isFound = await _registry.MarkClientReadyAsync(context.Message.CallId).ConfigureAwait(false);

        return new MarkRealtimeAiWebRtcClientReadyResponse { IsFound = isFound };
    }
}
