using System.Text;
using Microsoft.Extensions.Configuration;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Clients.Default;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.IntegrationTests.Mocks;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

public class RealtimeAiServiceResponseCreateIntegrationTests
{
    [Fact]
    public async Task ShouldNotSendConcurrentResponseCreate()
    {
        var clientWs = new MockWebSocket();

        clientWs.EnqueueMessage("{\"text\":\"first\"}");
        clientWs.EnqueueMessage("{\"text\":\"second\"}");

        var providerWs = new MockRealtimeAiWssClient();

        providerWs.EnqueueSendTriggeredMessage("{\"type\":\"session.updated\"}");

        providerWs.EnqueueSendTriggeredMessage("{\"type\":\"unknown.event\"}");

        providerWs.EnqueueSendTriggeredMessage("{\"type\":\"response.created\"}");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["OpenAi:ApiKey"] = "test-key",
                ["OpenAiForHk:ApiKey"] = "test-key-hk" 
            }!).Build();
        
        var switcher = new RealtimeAiSwitcher(
            [providerWs],
            [new DefaultRealtimeAiClientAdapter()],
            [new OpenAiRealtimeAiProviderAdapter(new OpenAiSettings(config))]);
        
        var sut = new RealtimeAiService(switcher, new InactivityTimerManager());

        var options = new RealtimeSessionOptions
        {
            WebSocket = clientWs,
            ClientConfig = new RealtimeAiClientConfig { Client = RealtimeAiClient.Default },
            ModelConfig = new RealtimeAiModelConfig
            {
                Provider = RealtimeAiProvider.OpenAi,
                ServiceUrl = "wss://api.openai.com/v1/realtime"
            },
            ConnectionProfile = new RealtimeAiConnectionProfile { ProfileId = "integration" },
            Region = RealtimeAiServerRegion.US
        };

        await sut.ConnectAsync(options, CancellationToken.None);
        var sentMessages = providerWs.SentMessages.Select(bytes => Encoding.UTF8.GetString(bytes)).ToList();
        sentMessages.Count(m => m.Contains("\"type\":\"response.create\"")).ShouldBe(1);
        sentMessages.Count(m => m.Contains("\"type\":\"conversation.item.create\"")).ShouldBe(2);
    }
}
