using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class KonwledgeCopyCommandHandler : ICommandHandler<KonwledgeCopyCommand, KonwledgeCopyResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public KonwledgeCopyCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<KonwledgeCopyResponse> Handle(IReceiveContext<KonwledgeCopyCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _aiSpeechAssistantService.KonwledgeCopyAsync(context.Message, cancellationToken).ConfigureAwait(false);
        
        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);
        
        return new KonwledgeCopyResponse
        {
            Data = @event.KnowledgeOldJsons
        };
    }
}