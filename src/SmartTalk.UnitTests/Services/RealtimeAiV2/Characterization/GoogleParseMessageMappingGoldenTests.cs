using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.Google;
using SmartTalk.Core.Settings.Google;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the GOOGLE adapter's RAW JSON → ParsedRealtimeAiProviderEvent mapping
/// (the second inference provider), by feeding raw frames through the REAL ParseMessage. No Google
/// ParseMessage routing test exists today. Migration step S3 normalizes both adapters into one Kind
/// union; Google's routing must not silently change.
///
/// NOTE: the modelTurn-text arm currently returns ResponseTextDelta with Data=null (a latent //todo in
/// the adapter). Pinning that exact current behavior makes any future fix a deliberate RED-then-GREEN
/// re-baseline rather than an accidental change.
/// </summary>
public class GoogleParseMessageMappingGoldenTests
{
    private static GoogleRealtimeAiProviderAdapter NewAdapter() =>
        new(new GoogleSettings(Substitute.For<IConfiguration>()));

    private static ParsedRealtimeAiProviderEvent Parse(string raw) => NewAdapter().ParseMessage(raw);

    [Fact]
    public void SetupComplete_IsSessionInitializedWithRawJson()
    {
        var raw = """{"setupComplete":{}}""";

        var parsed = Parse(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.SessionInitialized);
        parsed.Data.ShouldBe(raw);
    }

    [Fact]
    public void ServerContent_TurnComplete_IsResponseTurnCompleted()
    {
        Parse("""{"serverContent":{"turnComplete":true}}""").Type.ShouldBe(RealtimeAiWssEventType.ResponseTurnCompleted);
    }

    [Fact]
    public void ServerContent_Interrupted_IsSpeechDetected()
    {
        Parse("""{"serverContent":{"interrupted":true}}""").Type.ShouldBe(RealtimeAiWssEventType.SpeechDetected);
    }

    [Fact]
    public void ServerContent_ModelTurnInlineData_IsResponseAudioDeltaWithData()
    {
        var parsed = Parse("""{"serverContent":{"modelTurn":{"parts":[{"inlineData":{"data":"AAAA"}}]}}}""");

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseAudioDelta);
        parsed.Data.ShouldBeOfType<RealtimeAiWssAudioData>().Base64Payload.ShouldBe("AAAA");
    }

    [Fact]
    public void ServerContent_ModelTurnText_IsResponseTextDeltaWithNullData_CurrentTodoBehavior()
    {
        var parsed = Parse("""{"serverContent":{"modelTurn":{"parts":[{"text":"hi"}]}}}""");

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseTextDelta);
        parsed.Data.ShouldBeNull();   // pins the latent //todo; a fix flips this to RED deliberately
    }

    [Fact]
    public void UnrecognizedFrame_IsUnknown()
    {
        Parse("""{"foo":"bar"}""").Type.ShouldBe(RealtimeAiWssEventType.Unknown);
    }

    [Fact]
    public void MalformedJson_IsCriticalError()
    {
        var parsed = Parse("not-json{{{");

        parsed.Type.ShouldBe(RealtimeAiWssEventType.Error);
        parsed.Data.ShouldBeOfType<RealtimeAiErrorData>().IsCritical.ShouldBeTrue();
    }
}
