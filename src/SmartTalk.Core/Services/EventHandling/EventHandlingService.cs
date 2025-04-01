using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Events.AiSpeechAssistant;

namespace SmartTalk.Core.Services.EventHandling;

public interface IEventHandlingService : IScopedDependency
{
    public Task HandlingEventAsync(AiSpeechAssistantKnowledgeAddedEvent @event, CancellationToken cancellationToken);
}

public partial class EventHandlingService : IEventHandlingService
{
    private readonly SmartiesClient _smartiesClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public EventHandlingService(SmartiesClient smartiesClient, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _smartiesClient = smartiesClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }
}