using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Speechmatics;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class TranscriptionCallbackCommandHandler : ICommandHandler<TranscriptionCallbackCommand, TranscriptionCallbackResponse>
{
    private readonly ISpeechmaticsService _speechmaticsService;
    
    public TranscriptionCallbackCommandHandler(ISpeechmaticsService speechmaticsService)
    {
        _speechmaticsService = speechmaticsService;
    }

    public async Task<TranscriptionCallbackResponse> Handle(IReceiveContext<TranscriptionCallbackCommand> context, CancellationToken cancellationToken)
    {
        return await _speechmaticsService.TranscriptionCallbackAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
