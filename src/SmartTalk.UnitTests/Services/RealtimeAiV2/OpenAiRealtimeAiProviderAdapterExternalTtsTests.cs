using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class OpenAiRealtimeAiProviderAdapterExternalTtsTests
{
    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    private static RealtimeSessionOptions OptionsWithMiniMaxTts() =>
        new()
        {
            ModelConfig = new RealtimeAiModelConfig
            {
                Prompt = "you are helpful",
                Voice = "alloy",
                Tools = new List<object>()
            },
            TtsConfig = new RealtimeAiTtsConfig
            {
                ProviderType = RealtimeAiTtsProviderType.MiniMax
            }
        };

    private static JObject SerializeAsProduction(object payload) =>
        JObject.Parse(JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));

    [Fact]
    public void BuildSessionConfig_MiniMaxTts_UsesTextOnlyAndOmitsAudioOutput()
    {
        var json = SerializeAsProduction(NewAdapter().BuildSessionConfig(OptionsWithMiniMaxTts(), RealtimeAiAudioCodec.MULAW));

        json["session"]!["output_modalities"]!.Values<string>().ShouldBe(new[] { "text" });
        json["session"]!["audio"]!["input"].ShouldNotBeNull();
        json["session"]!["audio"]!["output"].ShouldBeNull();
    }

    [Fact]
    public void ParseMessage_ResponseOutputTextDelta_MapsToTextDelta()
    {
        var parsed = NewAdapter().ParseMessage("""{"type":"response.output_text.delta","delta":"hello"}""");

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseTextDelta);
        parsed.Data.ShouldBeOfType<RealtimeAiWssTextData>().Text.ShouldBe("hello");
    }

    [Fact]
    public void ParseMessage_ResponseOutputTextDone_MapsToTextDone()
    {
        var parsed = NewAdapter().ParseMessage("""{"type":"response.output_text.done","text":"hello world"}""");

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseTextDone);
        parsed.Data.ShouldBeOfType<RealtimeAiWssTextData>().Text.ShouldBe("hello world");
    }

    [Fact]
    public void ParseMessage_ResponseDoneWithMessageText_ExposesCompletedText()
    {
        const string raw = """
            {
              "type": "response.done",
              "response": {
                "output": [
                  {
                    "type": "message",
                    "content": [
                      { "type": "output_text", "text": "hello minimax" }
                    ]
                  }
                ]
              }
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseTurnCompleted);
        parsed.Data.ShouldBeOfType<RealtimeAiWssTextData>().Text.ShouldBe("hello minimax");
    }
}
