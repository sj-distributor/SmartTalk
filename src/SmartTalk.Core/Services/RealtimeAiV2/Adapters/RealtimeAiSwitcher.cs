using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters;

public interface IRealtimeAiSwitcher : IScopedDependency
{
    IRealtimeAiWssClient WssClient(AiSpeechAssistantProvider provider);
    
    IRealtimeAiProviderAdapter ProviderAdapter(AiSpeechAssistantProvider provider);
}

public class RealtimeAiSwitcher : IRealtimeAiSwitcher
{
    private readonly IEnumerable<IRealtimeAiWssClient> _wssClients;
    private readonly IEnumerable<IRealtimeAiProviderAdapter> _providerAdapters;

    public RealtimeAiSwitcher(IEnumerable<IRealtimeAiWssClient> wssClients, IEnumerable<IRealtimeAiProviderAdapter> providerAdapters)
    {
        _wssClients = wssClients;
        _providerAdapters = providerAdapters;
    }

    public IRealtimeAiWssClient WssClient(AiSpeechAssistantProvider provider) =>
        _wssClients.FirstOrDefault(x => x.Provider == provider) ?? throw new NullReferenceException("Wss Client: Provider not found");

    public IRealtimeAiProviderAdapter ProviderAdapter(AiSpeechAssistantProvider provider) =>
        _providerAdapters.FirstOrDefault(x => x.Provider == provider) ?? throw new NullReferenceException("Provider Adapter: Provider not found");
}