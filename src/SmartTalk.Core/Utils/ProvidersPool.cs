using Serilog;
using SmartTalk.Core.Services.Communication.Exceptions;

namespace SmartTalk.Core.Utils;

public static class ProvidersPool
{
    public static TProvider GetProvider<TProvider>(List<TProvider> providers, object provider = null, bool? random = true)
    {
        if (provider != null) return (TProvider)provider;

        provider = providers.Count switch
        {
            > 1 when random.HasValue && random.Value => providers[new Random().Next(0, providers.Count - 1)],
            < 1 => throw new MissingThirdPartyProviderException(),
            _ => providers.FirstOrDefault()
        };
        
        Log.Information($"{typeof(TProvider).Name}: {provider}");
        
        return (TProvider)provider;
    }
}