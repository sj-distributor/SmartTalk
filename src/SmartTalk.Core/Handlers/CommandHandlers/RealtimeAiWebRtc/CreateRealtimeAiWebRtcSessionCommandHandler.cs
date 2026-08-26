using Mediator.Net.Context;
using Mediator.Net.Contracts;
using Serilog;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
using SmartTalk.Messages.Commands.RealtimeAiWebRtc;

namespace SmartTalk.Core.Handlers.CommandHandlers.RealtimeAiWebRtc;

public class CreateRealtimeAiWebRtcSessionCommandHandler
    : ICommandHandler<CreateRealtimeAiWebRtcSessionCommand, CreateRealtimeAiWebRtcSessionResponse>
{
    private readonly IRealtimeAiWebRtcSessionRegistry _registry;
    private readonly IAiSpeechAssistantSessionCredentialService _credentialService;

    public CreateRealtimeAiWebRtcSessionCommandHandler(
        IRealtimeAiWebRtcSessionRegistry registry,
        IAiSpeechAssistantSessionCredentialService credentialService)
    {
        _registry = registry;
        _credentialService = credentialService;
    }

    public async Task<CreateRealtimeAiWebRtcSessionResponse> Handle(
        IReceiveContext<CreateRealtimeAiWebRtcSessionCommand> context,
        CancellationToken cancellationToken)
    {
        var command = context.Message;
        var reservationId = Guid.NewGuid().ToString("N");
        var reservationStatus = await _credentialService
            .ReserveWebRtcAsync(command.SessionId, reservationId, cancellationToken)
            .ConfigureAwait(false);

        if (reservationStatus == AiSpeechAssistantSessionWebRtcTransitionStatus.Conflict)
            return new CreateRealtimeAiWebRtcSessionResponse { IsSessionAlreadyBound = true };

        if (reservationStatus != AiSpeechAssistantSessionWebRtcTransitionStatus.Succeeded)
            throw new InvalidOperationException("Could not reserve the interview session for a WebRTC call.");

        RealtimeAiWebRtcCallResult result = null;
        try
        {
            result = await _registry.CreateAsync(
                command.AssistantId,
                command.Region,
                command.OfferSdp,
                cancellationToken).ConfigureAwait(false);

            var activationStatus = await _credentialService
                .ActivateWebRtcAsync(command.SessionId, reservationId, result.CallId, cancellationToken)
                .ConfigureAwait(false);
            if (activationStatus != AiSpeechAssistantSessionWebRtcTransitionStatus.Succeeded)
                throw new InvalidOperationException("Could not bind the interview session to the WebRTC call.");

            return new CreateRealtimeAiWebRtcSessionResponse
            {
                CallId = result.CallId,
                AnswerSdp = result.AnswerSdp
            };
        }
        catch
        {
            if (result != null)
            {
                try
                {
                    await _registry.StopAsync(result.CallId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warning(
                        ex,
                        "[RealtimeAiWebRtc] Failed to stop unbound call, CallId: {CallId}",
                        result.CallId);
                }
            }

            await _credentialService
                .ReleaseWebRtcReservationAsync(command.SessionId, reservationId, CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }
}
