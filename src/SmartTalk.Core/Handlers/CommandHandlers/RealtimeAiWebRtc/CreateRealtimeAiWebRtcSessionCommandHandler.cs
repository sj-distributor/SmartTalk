using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
using SmartTalk.Messages.Commands.RealtimeAiWebRtc;

namespace SmartTalk.Core.Handlers.CommandHandlers.RealtimeAiWebRtc;

public class CreateRealtimeAiWebRtcSessionCommandHandler
    : ICommandHandler<CreateRealtimeAiWebRtcSessionCommand, CreateRealtimeAiWebRtcSessionResponse>
{
    private readonly IRealtimeAiWebRtcSessionRegistry _registry;

    public CreateRealtimeAiWebRtcSessionCommandHandler(IRealtimeAiWebRtcSessionRegistry registry)
    {
        _registry = registry;
    }

    public async Task<CreateRealtimeAiWebRtcSessionResponse> Handle(
        IReceiveContext<CreateRealtimeAiWebRtcSessionCommand> context,
        CancellationToken cancellationToken)
    {
        var command = context.Message;
        var result = await _registry.CreateAsync(
            command.AssistantId,
            command.Region,
            command.OfferSdp,
            cancellationToken).ConfigureAwait(false);

        return new CreateRealtimeAiWebRtcSessionResponse
        {
            CallId = result.CallId,
            AnswerSdp = result.AnswerSdp
        };
    }
}
