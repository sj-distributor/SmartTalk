using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.HrInterView;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Events.AiSpeechAssistant;
using SmartTalk.Messages.Events.HrInterView;
using SmartTalk.Messages.Events.Pos;

namespace SmartTalk.Core.Services.EventHandling;

public interface IEventHandlingService : IScopedDependency
{
    public Task HandlingEventAsync(AiSpeechAssistantKnowledgeAddedEvent @event, CancellationToken cancellationToken);
    
    public Task HandlingEventAsync(PosOrderPlacedEvent @event, CancellationToken cancellationToken);
    
    public Task HandlingEventAsync(ConnectWebSocketEvent @event, CancellationToken cancellationToken);
}

public partial class EventHandlingService : IEventHandlingService
{
    private readonly IAsrClient _asrClient;
    private readonly SmartiesClient _smartiesClient;
    private readonly IPosDataProvider _posDataProvider;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IHrInterViewDataProvider _hrInterViewDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public EventHandlingService(IAsrClient asrClient, SmartiesClient smartiesClient, IPosDataProvider posDataProvider,ISmartTalkHttpClientFactory httpClientFactory, IHrInterViewDataProvider hrInterViewDataProvider, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _asrClient = asrClient;
        _smartiesClient = smartiesClient;
        _posDataProvider = posDataProvider;
        _hrInterViewDataProvider = hrInterViewDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }
}