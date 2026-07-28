using Newtonsoft.Json;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

internal static class AiSpeechAssistantFunctionCallHelper
{
    public const string InputAudioNoiseReductionName = "input_audio_noise_reduction";
    private const string DefaultNoiseReductionType = "near_field";

    public static async Task SetPhoneNoiseReductionAsync(
        this IAiSpeechAssistantDataProvider dataProvider,
        Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        bool enabled,
        CancellationToken cancellationToken)
    {
        await dataProvider.UpsertAssistantFunctionCallAsync(
            assistant,
            InputAudioNoiseReductionName,
            AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction,
            JsonConvert.SerializeObject(new { type = DefaultNoiseReductionType }),
            enabled,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task UpsertAssistantFunctionCallAsync(
        this IAiSpeechAssistantDataProvider dataProvider,
        Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        string name,
        AiSpeechAssistantSessionConfigType type,
        string content,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var existingFunctionCalls = await dataProvider.GetAiSpeechAssistantFunctionCallsAsync(
            [assistant.Id],
            [name],
            type,
            assistant.ModelProvider,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (existingFunctionCalls.Count == 0)
        {
            var functionCall = new AiSpeechAssistantFunctionCall
            {
                AssistantId = assistant.Id,
                Name = name,
                Content = content,
                Type = type,
                ModelProvider = assistant.ModelProvider,
                IsActive = isActive
            };

            await dataProvider
                .AddAiSpeechAssistantFunctionCallsAsync([functionCall], cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        foreach (var functionCall in existingFunctionCalls)
        {
            functionCall.IsActive = isActive;

            if (isActive && string.IsNullOrWhiteSpace(functionCall.Content))
                functionCall.Content = content;
        }

        await dataProvider
            .UpdateAiSpeechAssistantFunctionCallAsync(existingFunctionCalls, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public static bool HasPhoneChannel(this Domain.AISpeechAssistant.AiSpeechAssistant assistant)
    {
        if (string.IsNullOrWhiteSpace(assistant.Channel)) return false;

        return assistant.Channel
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Any(x =>
                x == ((int)AiSpeechAssistantChannel.PhoneChat).ToString() ||
                string.Equals(x, AiSpeechAssistantChannel.PhoneChat.ToString(), StringComparison.OrdinalIgnoreCase));
    }
}
