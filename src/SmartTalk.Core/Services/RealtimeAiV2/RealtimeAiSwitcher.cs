using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Wss;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2;

public interface IRealtimeAiSwitcher : IScopedDependency
{
    IRealtimeAiWssClient WssClient(RealtimeAiProvider provider);

    IRealtimeAiClientAdapter ClientAdapter(RealtimeAiClient client);
    
    IRealtimeAiProviderAdapter ProviderAdapter(RealtimeAiProvider provider);
}

public class RealtimeAiSwitcher : IRealtimeAiSwitcher
{
    private readonly IEnumerable<IRealtimeAiWssClient> _wssClients;
    private readonly IEnumerable<IRealtimeAiClientAdapter> _clientAdapters;
    private readonly IEnumerable<IRealtimeAiProviderAdapter> _providerAdapters;

    public RealtimeAiSwitcher(IEnumerable<IRealtimeAiWssClient> wssClients, IEnumerable<IRealtimeAiClientAdapter> clientAdapters, IEnumerable<IRealtimeAiProviderAdapter> providerAdapters)
    {
        _wssClients = wssClients;
        _clientAdapters = clientAdapters;
        _providerAdapters = providerAdapters;
    }

    public IRealtimeAiWssClient WssClient(RealtimeAiProvider provider) =>
        _wssClients.FirstOrDefault(x => x.Provider == provider) ?? throw new NullReferenceException("Wss Client: Provider not found");

    public IRealtimeAiClientAdapter ClientAdapter(RealtimeAiClient client) =>
        _clientAdapters.FirstOrDefault(x => x.Client == client) ?? throw new NullReferenceException("Client Adapter: Client not found");

    public IRealtimeAiProviderAdapter ProviderAdapter(RealtimeAiProvider provider) =>
        _providerAdapters.FirstOrDefault(x => x.Provider == provider) ?? throw new NullReferenceException("Provider Adapter: Provider not found");
}