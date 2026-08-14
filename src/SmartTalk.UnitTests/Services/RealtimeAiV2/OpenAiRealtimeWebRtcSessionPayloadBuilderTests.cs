using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class OpenAiRealtimeWebRtcSessionPayloadBuilderTests
{
    [Fact]
    public void Build_PreservesBusinessConfigAndEnablesNativeBargeIn()
    {
        var adapter = new OpenAiRealtimeAiProviderAdapter(
            new OpenAiSettings(new ConfigurationBuilder().Build()));
        var options = new RealtimeSessionOptions
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Provider = RealtimeAiProvider.OpenAi,
                ServiceUrl = "wss://api.openai.com/v1/realtime",
                ModelName = "gpt-realtime-test",
                Prompt = "same assistant prompt",
                Voice = "marin",
                Tools = new List<object>
                {
                    new { type = "function", name = "lookup_candidate" }
                },
                TurnDetection = new
                {
                    type = "server_vad",
                    threshold = 0.6,
                    silence_duration_ms = 450
                }
            },
            TtsConfig = new RealtimeAiTtsConfig
            {
                ProviderType = RealtimeAiTtsProviderType.BuiltIn
            }
        };

        var session = JObject.Parse(OpenAiRealtimeWebRtcSessionPayloadBuilder.Build(options, adapter));

        session.Value<string>("type").ShouldBe("realtime");
        session.Value<string>("model").ShouldBe("gpt-realtime-test");
        session.Value<string>("instructions").ShouldBe("same assistant prompt");
        session["audio"]?["output"]?.Value<string>("voice").ShouldBe("marin");
        session["tools"]?[0]?.Value<string>("name").ShouldBe("lookup_candidate");
        session["audio"]?["input"]?["turn_detection"]?.Value<double>("threshold").ShouldBe(0.6);
        session["audio"]?["input"]?["turn_detection"]?.Value<bool>("create_response").ShouldBeTrue();
        session["audio"]?["input"]?["turn_detection"]?.Value<bool>("interrupt_response").ShouldBeTrue();
        session["session"].ShouldBeNull();
    }

    [Fact]
    public void Build_ExternalTts_IsRejectedWithoutChangingLegacyPath()
    {
        var adapter = new OpenAiRealtimeAiProviderAdapter(
            new OpenAiSettings(new ConfigurationBuilder().Build()));
        var options = new RealtimeSessionOptions
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Provider = RealtimeAiProvider.OpenAi,
                ModelName = "gpt-realtime-test"
            },
            TtsConfig = new RealtimeAiTtsConfig
            {
                ProviderType = RealtimeAiTtsProviderType.MiniMax
            }
        };

        Should.Throw<NotSupportedException>(() =>
            OpenAiRealtimeWebRtcSessionPayloadBuilder.Build(options, adapter));
    }

    [Fact]
    public void BuildSidebandUri_UsesSameCallId()
    {
        OpenAiRealtimeWebRtcCallClient.ResolveBaseUrl(
                "wss://api.openai.com/v1/realtime?model=gpt-realtime-test")
            .ShouldBe("https://api.openai.com");

        var uri = OpenAiRealtimeWebRtcCallClient.BuildSidebandUri(
            "https://api.openai.com",
            "rtc_test_123");

        uri.ToString().ShouldBe("wss://api.openai.com/v1/realtime?call_id=rtc_test_123");
        OpenAiRealtimeWebRtcCallClient.ParseCallId("/v1/realtime/calls/rtc_test_123")
            .ShouldBe("rtc_test_123");
    }
}
