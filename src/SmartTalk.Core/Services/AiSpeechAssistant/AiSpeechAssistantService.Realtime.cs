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
        var (session, ephemeralToken) = await InitialRealtimeSessionsAsync(command, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(ephemeralToken)) throw new Exception("Invalid ephemeral token");
        
        var answerSdp = await _openaiClient.RealtimeChatAsync(command.OfferSdp, ephemeralToken, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Create realtime connection response: {@Response}" , answerSdp);

        return new CreateRealtimeConnectionResponse
        {
            Data = new CreateRealtimeConnectionResponseData
            {
                AnswerSdp = answerSdp,
                Session = session
            }
        };
    }

    private async Task<(OpenAiRealtimeSessionDto Session, string EphemeralToken)> InitialRealtimeSessionsAsync(CreateRealtimeConnectionCommand command, CancellationToken cancellationToken)
    {
        var prompt = await GenerateFinalPromptAsync(command, cancellationToken).ConfigureAwait(false);

        var assistant = command.AssistantId.HasValue
            ? await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantAsync(command.AssistantId.Value, cancellationToken).ConfigureAwait(false)
            : null;
        
        var configs = assistant == null ? [] : await InitialSessionConfigAsync(assistant, cancellationToken).ConfigureAwait(false);

        var session = new OpenAiRealtimeSessionDto
        {
            Model = "gpt-4o-realtime-preview-2024-12-17",
            TurnDetection = InitialSessionParameters(configs, AiSpeechAssistantSessionConfigType.TurnDirection),
            Voice = string.IsNullOrEmpty(assistant?.ModelVoice) ? "alloy" : assistant.ModelVoice,
            Instructions = prompt,
            Modalities = ["audio", "text"],
            InputAudioTranscription = new { model = "whisper-1" },
            Tools = configs.Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool).Select(x => x.Config).ToList()
        };

        var ephemeralToken = await _openaiClient.InitialRealtimeSessionsAsync(session, cancellationToken).ConfigureAwait(false);

        return (session, ephemeralToken);
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