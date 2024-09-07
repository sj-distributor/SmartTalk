using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class TranscriptionCallbackCommandHandler : ICommandHandler<HandleTranscriptionCallbackCommand>
{
    private readonly ISpeechMaticsService _speechMaticsService;
    
    public TranscriptionCallbackCommandHandler(ISpeechMaticsService speechMaticsService)
    {
        _speechMaticsService = speechMaticsService;
    }

    public async Task Handle(IReceiveContext<HandleTranscriptionCallbackCommand> context, CancellationToken cancellationToken)
    {
        await _speechMaticsService.HandleTranscriptionCallbackAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
