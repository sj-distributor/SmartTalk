using Serilog;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.OpenAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<CreateRealtimeConnectionResponse> CreateRealtimeConnectionAsync(CreateRealtimeConnectionCommand command, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<CreateRealtimeConnectionResponse> CreateRealtimeConnectionAsync(CreateRealtimeConnectionCommand command, CancellationToken cancellationToken)
    {
        var ephemeralToken = await InitialRealtimeSessionsAsync(command, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(ephemeralToken)) throw new Exception("Invalid ephemeral token");
        
        var sdpAnswer = await _openaiClient.RealtimeChatAsync(command.OfferSdp, ephemeralToken, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Create rtc connection response: {@Response}" , sdpAnswer);

        return new CreateRealtimeConnectionResponse
        {
            Data = sdpAnswer
        };
    }

    private async Task<string> InitialRealtimeSessionsAsync(CreateRealtimeConnectionCommand command, CancellationToken cancellationToken)
    {
        var prompt = await GenerateFinalPromptAsync(command, cancellationToken).ConfigureAwait(false);

        var assistant = command.AssistantId.HasValue
            ? await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantAsync(command.AssistantId.Value, cancellationToken).ConfigureAwait(false)
            : null;
        
        var configs = assistant == null ? [] : await InitialSessionConfigAsync(assistant, cancellationToken).ConfigureAwait(false);

        var session = new OpenAiRealtimeSessionsInitialRequestDto
        {
            Model = string.IsNullOrEmpty(assistant?.ModelUrl) ? "gpt-4o-realtime-preview-2024-12-1" : assistant.ModelUrl,
            TurnDetection = InitialSessionTurnDirection(configs),
            InputAudioFormat = "g711_ulaw",
            OutputAudioFormat = "g711_ulaw",
            Voice = string.IsNullOrEmpty(assistant?.ModelVoice) ? "alloy" : assistant.ModelVoice,
            Instructions = prompt,
            Modalities = ["text", "audio"],
            Temperature = 0.8,
            InputAudioTranscription = new { model = "whisper-1" },
            Tools = configs.Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool).Select(x => x.Config).ToList()
        };

        return await _openaiClient.InitialRealtimeSessionsAsync(session, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GenerateFinalPromptAsync(CreateRealtimeConnectionCommand command, CancellationToken cancellationToken)
    {
        var prompt = string.IsNullOrWhiteSpace(command.CustomPrompt) ? "You are a friendly assistant" : command.CustomPrompt;

        if (!command.AssistantId.HasValue) return prompt;
        
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
            assistantId: command.AssistantId.Value, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (knowledge == null) 
            throw new Exception($"Could not found the knowledge by id: {command.AssistantId.Value}");

        if (!string.IsNullOrWhiteSpace(command.CustomPrompt) && !string.IsNullOrWhiteSpace(knowledge.Prompt))
            prompt += $"\n\n{knowledge.Prompt}";
        else
            prompt = knowledge.Prompt;

        return prompt;
    }
}