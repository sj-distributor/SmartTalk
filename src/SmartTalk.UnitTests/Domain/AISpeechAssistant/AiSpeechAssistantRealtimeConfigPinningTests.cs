using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Shouldly;
using SmartTalk.Core.Domain.AISpeechAssistant;
using Xunit;

namespace SmartTalk.UnitTests.Domain.AISpeechAssistant;

/// <summary>
/// Pins the column-name strings and CLR types of every Realtime API session-config
/// property added in Phase 4.1 of the Round 2 stability rollout. These properties
/// map 1:1 to NULLABLE columns in the <c>ai_speech_assistant</c> table; renaming
/// either side desynchronises them and silently breaks per-assistant configuration
/// (Phase 5+) for every row that has a non-null value.
///
/// <para>
/// All properties MUST be nullable reference types (<c>string</c>) or nullable
/// value types (<c>int?</c>, <c>decimal?</c>) — a non-nullable type here would
/// silently coerce a NULL DB column to the default (0 / "") and a default would
/// then be treated by Phase 4.2 adapter logic as an opt-in value, changing the
/// outbound session.update payload for every existing assistant. NULLABLE is
/// load-bearing — pin it.
/// </para>
///
/// <para>
/// A failure on this test means a renamed property or a flipped nullability —
/// either is a production-grade incident if it merges. Fix the rename, do not
/// edit this test to match.
/// </para>
/// </summary>
public class AiSpeechAssistantRealtimeConfigPinningTests
{
    [Theory]
    [InlineData(nameof(AiSpeechAssistant.TranscriptionModel),        "transcription_model",        typeof(string),   true)]
    [InlineData(nameof(AiSpeechAssistant.TranscriptionLanguage),     "transcription_language",     typeof(string),   true)]
    [InlineData(nameof(AiSpeechAssistant.TurnDetectionType),         "turn_detection_type",        typeof(string),   true)]
    [InlineData(nameof(AiSpeechAssistant.TurnDetectionThreshold),    "turn_detection_threshold",   typeof(decimal), false)]
    [InlineData(nameof(AiSpeechAssistant.TurnDetectionSilenceMs),    "turn_detection_silence_ms",  typeof(int),     false)]
    [InlineData(nameof(AiSpeechAssistant.InputNoiseReductionType),   "input_noise_reduction_type", typeof(string),   true)]
    [InlineData(nameof(AiSpeechAssistant.MaxResponseOutputTokens),   "max_response_output_tokens", typeof(int),     false)]
    [InlineData(nameof(AiSpeechAssistant.OutputAudioSpeed),          "output_audio_speed",         typeof(decimal), false)]
    public void RealtimeConfigProperty_HasExpectedColumnNameAndNullableType(
        string propertyName, string expectedColumnName, Type underlyingType, bool isReferenceType)
    {
        var property = typeof(AiSpeechAssistant).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        property.ShouldNotBeNull($"Property {propertyName} was removed or renamed; Phase 5+ adapter wiring will break silently");

        var columnAttribute = property!.GetCustomAttribute<ColumnAttribute>();
        columnAttribute.ShouldNotBeNull($"Property {propertyName} lost its [Column] attribute — DB mapping will fall back to PascalCase and break the migration contract");
        columnAttribute!.Name.ShouldBe(expectedColumnName, $"Property {propertyName} column-name pin changed; DB column and CLR property are now desynchronised");

        // Nullable contract: value types must be Nullable<T>, reference types must allow null.
        // A regression here is a behavioural break: a NULL DB column would coerce to the default
        // (0 / "") and Phase 4.2 logic would treat that as an opt-in value.
        if (isReferenceType)
        {
            property.PropertyType.ShouldBe(underlyingType, $"Property {propertyName} type changed from a reference type; nullable contract broken");
        }
        else
        {
            var expectedNullableType = typeof(Nullable<>).MakeGenericType(underlyingType);
            property.PropertyType.ShouldBe(expectedNullableType, $"Property {propertyName} lost its Nullable<{underlyingType.Name}> wrapper — DB NULL will now coerce to default value, breaking the zero-behaviour-change contract");
        }
    }

    [Fact]
    public void RealtimeConfigProperties_DefaultValueIsNull()
    {
        // Constructing a fresh entity must leave every new config property at null —
        // otherwise existing prod rows (no migration backfill) would be indistinguishable
        // from "operator set this value" and Phase 4.2 logic would change session.update
        // shape for every assistant. This is the load-bearing invariant for the entire
        // Round 2 rollout: NULL means "behave exactly like today".
        var assistant = new AiSpeechAssistant();

        assistant.TranscriptionModel.ShouldBeNull();
        assistant.TranscriptionLanguage.ShouldBeNull();
        assistant.TurnDetectionType.ShouldBeNull();
        assistant.TurnDetectionThreshold.ShouldBeNull();
        assistant.TurnDetectionSilenceMs.ShouldBeNull();
        assistant.InputNoiseReductionType.ShouldBeNull();
        assistant.MaxResponseOutputTokens.ShouldBeNull();
        assistant.OutputAudioSpeed.ShouldBeNull();
    }
}
