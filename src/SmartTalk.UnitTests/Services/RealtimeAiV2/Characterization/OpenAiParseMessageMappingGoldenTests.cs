using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the OpenAI adapter's RAW JSON → ParsedRealtimeAiProviderEvent mapping
/// for the function-call, lifecycle, transcription and fallback arms, by feeding raw frames through the
/// REAL <see cref="OpenAiRealtimeAiProviderAdapter.ParseMessage"/>. Service-level tests stub ParseMessage
/// to return hand-built events, so these arms are otherwise unpinned. Migration step S3 normalizes this
/// into a Kind union and rewrites the mapping; every Type, field-source (delta vs transcript), Speaker
/// tag, item id and the fallback arms must survive, or these fail RED.
/// </summary>
public class OpenAiParseMessageMappingGoldenTests
{
    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(Substitute.For<IConfiguration>()));

    private static ParsedRealtimeAiProviderEvent Parse(string raw) => NewAdapter().ParseMessage(raw);

    // ── P12: function-call extraction ──────────────────────────────

    [Fact]
    public void ResponseDone_SingleFunctionCall_ExtractsCallIdNameArguments()
    {
        var parsed = Parse("""{"type":"response.done","response":{"output":[{"type":"function_call","name":"lookup","call_id":"call_1","arguments":"{\"q\":\"x\"}"}]}}""");

        parsed.Type.ShouldBe(RealtimeAiWssEventType.FunctionCallSuggested);
        var calls = parsed.Data.ShouldBeOfType<List<RealtimeAiWssFunctionCallData>>();
        calls.Count.ShouldBe(1);
        calls[0].CallId.ShouldBe("call_1");
        calls[0].FunctionName.ShouldBe("lookup");
        calls[0].ArgumentsJson.ShouldBe("""{"q":"x"}""");
    }

    [Fact]
    public void ResponseDone_MultipleFunctionCalls_PreservesOutputArrayOrder()
    {
        var parsed = Parse("""{"type":"response.done","response":{"output":[{"type":"function_call","name":"a","call_id":"ca","arguments":"{}"},{"type":"function_call","name":"b","call_id":"cb","arguments":"{}"}]}}""");

        var calls = parsed.Data.ShouldBeOfType<List<RealtimeAiWssFunctionCallData>>();
        calls.Select(c => c.FunctionName).ShouldBe(new[] { "a", "b" });
    }

    [Fact]
    public void ResponseDone_FunctionCallWithoutName_IsSkipped()
    {
        var parsed = Parse("""{"type":"response.done","response":{"output":[{"type":"function_call","call_id":"c0","arguments":"{}"},{"type":"function_call","name":"keep","call_id":"c1","arguments":"{}"}]}}""");

        var calls = parsed.Data.ShouldBeOfType<List<RealtimeAiWssFunctionCallData>>();
        calls.Select(c => c.FunctionName).ShouldBe(new[] { "keep" });
    }

    [Fact]
    public void ResponseDone_EmptyOutput_IsTurnCompletedNotFunctionCall()
    {
        var parsed = Parse("""{"type":"response.done","response":{"output":[]}}""");

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseTurnCompleted);
        parsed.Data.ShouldBeNull();
    }

    // ── P13: lifecycle / control / transcription mapping ───────────

    [Fact]
    public void Lifecycle_SessionUpdated_IsSessionInitializedWithRawJson()
    {
        var raw = """{"type":"session.updated","session":{}}""";
        var parsed = Parse(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.SessionInitialized);
        parsed.Data.ShouldBe(raw);
    }

    [Theory]
    [InlineData("""{"type":"response.created"}""", RealtimeAiWssEventType.ResponseStarted)]
    [InlineData("""{"type":"input_audio_buffer.speech_started"}""", RealtimeAiWssEventType.SpeechDetected)]
    [InlineData("""{"type":"response.output_audio.done"}""", RealtimeAiWssEventType.ResponseAudioDone)]
    public void Lifecycle_ControlEvents_MapToExpectedType(string raw, RealtimeAiWssEventType expected)
    {
        Parse(raw).Type.ShouldBe(expected);
    }

    [Fact]
    public void AudioDelta_ReadsDeltaPayloadAndItemId()
    {
        var parsed = Parse("""{"type":"response.output_audio.delta","delta":"AAAA","item_id":"it1"}""");

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseAudioDelta);
        parsed.ItemId.ShouldBe("it1");
        var audio = parsed.Data.ShouldBeOfType<RealtimeAiWssAudioData>();
        audio.Base64Payload.ShouldBe("AAAA");
        audio.ItemId.ShouldBe("it1");
    }

    [Theory]
    // input transcription: partial reads "delta", completed reads "transcript", Speaker = User
    [InlineData("""{"type":"conversation.item.input_audio_transcription.delta","delta":"你"}""", RealtimeAiWssEventType.InputAudioTranscriptionPartial, "你", AiSpeechAssistantSpeaker.User)]
    [InlineData("""{"type":"conversation.item.input_audio_transcription.completed","transcript":"你好"}""", RealtimeAiWssEventType.InputAudioTranscriptionCompleted, "你好", AiSpeechAssistantSpeaker.User)]
    // output transcription: Speaker = Ai
    [InlineData("""{"type":"response.output_audio_transcript.delta","delta":"Hi"}""", RealtimeAiWssEventType.OutputAudioTranscriptionPartial, "Hi", AiSpeechAssistantSpeaker.Ai)]
    [InlineData("""{"type":"response.output_audio_transcript.done","transcript":"Hi there"}""", RealtimeAiWssEventType.OutputAudioTranscriptionCompleted, "Hi there", AiSpeechAssistantSpeaker.Ai)]
    public void Transcription_MapsTypeFieldSourceAndSpeaker(string raw, RealtimeAiWssEventType expectedType, string expectedTranscript, AiSpeechAssistantSpeaker expectedSpeaker)
    {
        var parsed = Parse(raw);

        parsed.Type.ShouldBe(expectedType);
        var data = parsed.Data.ShouldBeOfType<RealtimeAiWssTranscriptionData>();
        data.Transcript.ShouldBe(expectedTranscript);
        data.Speaker.ShouldBe(expectedSpeaker);
    }

    // ── P14: fallback arms ─────────────────────────────────────────

    [Fact]
    public void UnknownEventName_IsUnknownWithEventTypeData()
    {
        var parsed = Parse("""{"type":"response.some_future_event"}""");

        parsed.Type.ShouldBe(RealtimeAiWssEventType.Unknown);
        parsed.Data.ShouldBe("response.some_future_event");
    }

    [Fact]
    public void MissingTypeField_IsUnknown()
    {
        Parse("""{"foo":"bar"}""").Type.ShouldBe(RealtimeAiWssEventType.Unknown);
    }

    [Fact]
    public void MalformedJson_IsCriticalError_NotSwallowedAsUnknown()
    {
        var parsed = Parse("not-json{{{");

        parsed.Type.ShouldBe(RealtimeAiWssEventType.Error);
        parsed.Data.ShouldBeOfType<RealtimeAiErrorData>().IsCritical.ShouldBeTrue();
    }
}
