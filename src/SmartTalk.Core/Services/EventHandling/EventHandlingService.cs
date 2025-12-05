using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Messages.Events.AiSpeechAssistant;
using SmartTalk.Messages.Events.Pos;

namespace SmartTalk.Core.Services.EventHandling;

public interface IEventHandlingService : IScopedDependency
{
    public Task HandlingEventAsync(AiSpeechAssistantKnowledgeAddedEvent @event, CancellationToken cancellationToken);
    
    public Task HandlingEventAsync(PosOrderPlacedEvent @event, CancellationToken cancellationToken);
}

public partial class EventHandlingService : IEventHandlingService
{
    private readonly SmartiesClient _smartiesClient;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public EventHandlingService(SmartiesClient smartiesClient, IPosDataProvider posDataProvider, IPhoneOrderDataProvider phoneOrderDataProvider, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _smartiesClient = smartiesClient;
        _posDataProvider = posDataProvider;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }
}