using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.RealtimeAi.wss;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.Adapters;

public interface IRealtimeAiSwitcher : IScopedDependency
{
    IRealtimeAiWssClient WssClient(RealtimeAiProvider provider);
    
    IRealtimeAiProviderAdapter ProviderAdapter(RealtimeAiProvider provider);
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

    public IRealtimeAiWssClient WssClient(RealtimeAiProvider provider) =>
        _wssClients.FirstOrDefault(x => x.Provider == provider) ?? throw new NullReferenceException("Wss Client: Provider not found");

    public IRealtimeAiProviderAdapter ProviderAdapter(RealtimeAiProvider provider) =>
        _providerAdapters.FirstOrDefault(x => x.Provider == provider) ?? throw new NullReferenceException("Provider Adapter: Provider not found");
}