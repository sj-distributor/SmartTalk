using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class TranscriptionCallbackCommandHandler : ICommandHandler<HandleTranscriptionCallbackCommand, TranscriptionCallbackHandledResponse>
{
    private readonly ISpeechMaticsService _speechMaticsService;
    
    public TranscriptionCallbackCommandHandler(ISpeechMaticsService speechMaticsService)
    {
        _speechMaticsService = speechMaticsService;
    }

    public async Task<TranscriptionCallbackHandledResponse> Handle(IReceiveContext<HandleTranscriptionCallbackCommand> context, CancellationToken cancellationToken)
    {
        return await _speechMaticsService.HandleTranscriptionCallbackAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
