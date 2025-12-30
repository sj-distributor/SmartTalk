using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.HrInterView;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Events.AiSpeechAssistant;
using SmartTalk.Messages.Events.HrInterView;
using SmartTalk.Messages.Events.PhoneOrder;
using SmartTalk.Messages.Events.Pos;

namespace SmartTalk.Core.Services.EventHandling;

public interface IEventHandlingService : IScopedDependency
{
    Task HandlingEventAsync(AiSpeechAssistantKnowledgeAddedEvent @event, CancellationToken cancellationToken);
    Task HandlingEventAsync(PosOrderPlacedEvent @event, CancellationToken cancellationToken);
    
    Task HandlingEventAsync(ConnectWebSocketEvent @event, CancellationToken cancellationToken);
    
    Task HandlingEventAsync(PhoneOrderRecordUpdatedEvent @event, CancellationToken cancellationToken);
    
    public Task HandlingEventAsync(AiSpeechAssistantKonwledgeCopyAddedEvent @event, CancellationToken cancellationToken);
}

public partial class EventHandlingService : IEventHandlingService
{
    private readonly IAsrClient _asrClient;
    private readonly SmartiesClient _smartiesClient;
    private readonly IPosUtilService _posUtilService;
    private readonly IPosDataProvider _posDataProvider;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IHrInterViewDataProvider _hrInterViewDataProvider;
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public EventHandlingService(IAsrClient asrClient, SmartiesClient smartiesClient, IPosUtilService posUtilService, IPosDataProvider posDataProvider, ISmartTalkHttpClientFactory httpClientFactory, IPhoneOrderDataProvider phoneOrderDataProvider, IHrInterViewDataProvider hrInterViewDataProvider, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider, IAiSpeechAssistantService aiSpeechAssistantService) 
    {
        _asrClient = asrClient;
        _smartiesClient = smartiesClient;
        _posUtilService = posUtilService;
        _posDataProvider = posDataProvider;
        _httpClientFactory = httpClientFactory;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _hrInterViewDataProvider = hrInterViewDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }
}