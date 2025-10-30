using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class SyncAiSpeechAssistantInfoToAgentCommandHandler : ICommandHandler<SyncAiSpeechAssistantInfoToAgentCommand>
{
    private readonly IAiSpeechAssistantProcessJobService _processJobService;

    public SyncAiSpeechAssistantInfoToAgentCommandHandler(IAiSpeechAssistantProcessJobService processJobService)
    {
        _processJobService = processJobService;
    }

    public async Task Handle(IReceiveContext<SyncAiSpeechAssistantInfoToAgentCommand> context, CancellationToken cancellationToken)
    {
        await _processJobService.SyncAiSpeechAssistantInfoToAgentAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}