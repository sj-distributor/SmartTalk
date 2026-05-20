using Microsoft.Extensions.Configuration;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

internal static class OpenAiRealtimeAiProviderAdapterTestSettings
{
    public static IConfiguration BuildConfiguration()
    {
        var data = new Dictionary<string, string>
        {
            ["OpenAi:BaseUrl"] = "https://example.test",
            ["OpenAi:ApiKey"] = "test-key",
            ["OpenAi:Organization"] = "test-org",
            ["OpenAi:Realtime:RealtimeSendBuffLength"] = "1024",
            ["OpenAi:Realtime:ReceiveBufferLength"] = "1024",
            ["OpenAi:Realtime:Temperature"] = "0.2",
            ["OpenAi:RealTimeApiKeys"] = "key1,key2",
            ["OpenAiForHk:BaseUrl"] = "https://example-hk.test",
            ["OpenAiForHk:ApiKey"] = "test-hk-key",
            ["OpenAiForHk:Organization"] = "test-hk-org"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }
}
