using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class SyncAiSpeechAssistantLanguageCommandHandler : ICommandHandler<SyncAiSpeechAssistantLanguageCommand>
{
    private readonly IAiSpeechAssistantProcessJobService _processJobService;

    public SyncAiSpeechAssistantLanguageCommandHandler(IAiSpeechAssistantProcessJobService processJobService)
    {
        _processJobService = processJobService;
    }

    public async Task Handle(IReceiveContext<SyncAiSpeechAssistantLanguageCommand> context, CancellationToken cancellationToken)
    {
        await _processJobService.SyncAiSpeechAssistantLanguageAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
