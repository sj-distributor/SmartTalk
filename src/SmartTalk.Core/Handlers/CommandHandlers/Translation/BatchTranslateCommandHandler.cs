using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Translation;
using SmartTalk.Messages.Commands.Translation;

namespace SmartTalk.Core.Handlers.CommandHandlers.Translation;

public class BatchTranslateCommandHandler : ICommandHandler<BatchTranslateCommand, BatchTranslateResponse>
{
    private readonly ITranslationService _translationService;

    public BatchTranslateCommandHandler(ITranslationService translationService)
    {
        _translationService = translationService;
    }

    public async Task<BatchTranslateResponse> Handle(IReceiveContext<BatchTranslateCommand> context, CancellationToken cancellationToken)
    {
        return await _translationService.BatchTranslateAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
