using Shouldly;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.AiSpeechAssistant;

/// <summary>
/// Integration coverage for the Phase 4.1 NULLABLE Realtime API session-config columns
/// added in <c>Script0007_add_realtime_config_fields_to_ai_speech_assistant.sql</c>.
///
/// <para>
/// These tests pin the persistence contract: NULL values must round-trip as NULL (the
/// load-bearing invariant for "default = today's behaviour" through Phase 5+), and
/// every value type must persist + read back identically across the EF Core ↔ MySQL
/// boundary. Decimal precision in particular is fragile — <c>turn_detection_threshold</c>
/// is <c>DECIMAL(4,3)</c> and <c>output_audio_speed</c> is <c>DECIMAL(3,2)</c>; a
/// schema-CLR precision mismatch would silently truncate operator-configured values.
/// </para>
/// </summary>
public partial class AiSpeechAssistantConnectFixture
{
    [Fact]
    public async Task ShouldPersistRealtimeConfig_AllNull_RoundTripsAsNull()
    {
        // The load-bearing invariant: fresh rows (no operator config) must round-trip
        // every new column as NULL — Phase 4.2 logic treats NULL as "use today's default".
        // If any column coerced to a non-null default on persist, Phase 5+ would activate
        // opt-in behaviour for every existing prod assistant on Phase 4.2 deploy.
        int assistantId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "ConfigNullAssistant",
                AnsweringNumber = TestDidNumber,
                ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy",
                IsDefault = true,
                IsDisplay = true
            };

            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            assistantId = assistant.Id;
        });

        await Run<IRepository>(async repository =>
        {
            var persisted = await repository.GetByIdAsync<Core.Domain.AISpeechAssistant.AiSpeechAssistant>(assistantId);

            persisted.ShouldNotBeNull();
            persisted.TranscriptionModel.ShouldBeNull();
            persisted.TranscriptionLanguage.ShouldBeNull();
            persisted.TurnDetectionType.ShouldBeNull();
            persisted.TurnDetectionThreshold.ShouldBeNull();
            persisted.TurnDetectionSilenceMs.ShouldBeNull();
            persisted.InputNoiseReductionType.ShouldBeNull();
            persisted.MaxResponseOutputTokens.ShouldBeNull();
            persisted.OutputAudioSpeed.ShouldBeNull();
        });
    }

    [Fact]
    public async Task ShouldPersistRealtimeConfig_AllPopulated_RoundTripsValuesIdentically()
    {
        // Values chosen to stress decimal precision boundaries:
        //   turn_detection_threshold (DECIMAL(4,3)) → 0.500 — 3 fractional digits exactly
        //   output_audio_speed       (DECIMAL(3,2)) → 1.15  — 2 fractional digits at upper UX range
        // A truncation here would corrupt operator config silently.
        int assistantId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "ConfigPopulatedAssistant",
                AnsweringNumber = TestDidNumber,
                ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy",
                IsDefault = true,
                IsDisplay = true,
                TranscriptionModel = "gpt-4o-transcribe",
                TranscriptionLanguage = "yue",
                TurnDetectionType = "semantic_vad",
                TurnDetectionThreshold = 0.500m,
                TurnDetectionSilenceMs = 700,
                InputNoiseReductionType = "near_field",
                MaxResponseOutputTokens = 1200,
                OutputAudioSpeed = 1.15m
            };

            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            assistantId = assistant.Id;
        });

        await Run<IRepository>(async repository =>
        {
            var persisted = await repository.GetByIdAsync<Core.Domain.AISpeechAssistant.AiSpeechAssistant>(assistantId);

            persisted.ShouldNotBeNull();
            persisted.TranscriptionModel.ShouldBe("gpt-4o-transcribe");
            persisted.TranscriptionLanguage.ShouldBe("yue");
            persisted.TurnDetectionType.ShouldBe("semantic_vad");
            persisted.TurnDetectionThreshold.ShouldBe(0.500m);
            persisted.TurnDetectionSilenceMs.ShouldBe(700);
            persisted.InputNoiseReductionType.ShouldBe("near_field");
            persisted.MaxResponseOutputTokens.ShouldBe(1200);
            persisted.OutputAudioSpeed.ShouldBe(1.15m);
        });
    }

    [Fact]
    public async Task ShouldPersistRealtimeConfig_PartialPopulation_OnlyPopulatedFieldsRetained()
    {
        // The realistic operator pattern: set ONE knob (e.g. just the language hint)
        // and leave the rest at default. Pin that mixed null + non-null state survives
        // the persistence round-trip, because mixed state is what every Phase 5+ canary
        // assistant will look like in prod.
        int assistantId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var assistant = new Core.Domain.AISpeechAssistant.AiSpeechAssistant
            {
                Name = "ConfigPartialAssistant",
                AnsweringNumber = TestDidNumber,
                ModelProvider = RealtimeAiProvider.OpenAi,
                ModelVoice = "alloy",
                IsDefault = true,
                IsDisplay = true,
                TranscriptionLanguage = "zh"        // only the lang hint is set
            };

            await repository.InsertAsync(assistant);
            await unitOfWork.SaveChangesAsync();

            assistantId = assistant.Id;
        });

        await Run<IRepository>(async repository =>
        {
            var persisted = await repository.GetByIdAsync<Core.Domain.AISpeechAssistant.AiSpeechAssistant>(assistantId);

            persisted.ShouldNotBeNull();
            persisted.TranscriptionLanguage.ShouldBe("zh");

            // Every other knob stays NULL — Phase 4.2 must continue to treat this
            // assistant as "default behaviour for everything except language hint".
            persisted.TranscriptionModel.ShouldBeNull();
            persisted.TurnDetectionType.ShouldBeNull();
            persisted.TurnDetectionThreshold.ShouldBeNull();
            persisted.TurnDetectionSilenceMs.ShouldBeNull();
            persisted.InputNoiseReductionType.ShouldBeNull();
            persisted.MaxResponseOutputTokens.ShouldBeNull();
            persisted.OutputAudioSpeed.ShouldBeNull();
        });
    }
}
