using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Messages.Events.AiSpeechAssistant;
using SmartTalk.Messages.Events.PhoneOrder;
using SmartTalk.Messages.Events.Pos;

namespace SmartTalk.Core.Services.EventHandling;

public interface IEventHandlingService : IScopedDependency
{
    Task HandlingEventAsync(AiSpeechAssistantKnowledgeAddedEvent @event, CancellationToken cancellationToken);
    
    Task HandlingEventAsync(PosOrderPlacedEvent @event, CancellationToken cancellationToken);
    
     Task HandlingEventAsync(PhoneOrderRecordUpdatedEvent @event, CancellationToken cancellationToken);
}

public partial class EventHandlingService : IEventHandlingService
{
    private readonly ICurrentUser _currentUser;
    private readonly SmartiesClient _smartiesClient;
    private readonly IPosUtilService _posUtilService;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IPhoneOrderUtilService _phoneOrderUtilService;
    private readonly ISpeechMaticsService _speechMaticsService;

    public EventHandlingService(SmartiesClient smartiesClient, IPosUtilService posUtilService, IPosDataProvider posDataProvider, IPhoneOrderDataProvider phoneOrderDataProvider, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider, IAgentDataProvider agentDataProvider, IPhoneOrderUtilService phoneOrderUtilService, ICurrentUser currentUser, ISpeechMaticsService speechMaticsService)
    {
        _smartiesClient = smartiesClient;
        _posUtilService = posUtilService;
        _posDataProvider = posDataProvider;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _agentDataProvider = agentDataProvider;
        _phoneOrderUtilService = phoneOrderUtilService;
        _currentUser = currentUser;
        _speechMaticsService = speechMaticsService;
    }
}