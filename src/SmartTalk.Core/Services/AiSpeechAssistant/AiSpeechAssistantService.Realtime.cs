using Serilog;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.OpenAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<CreateRealtimeConnectionResponse> CreateRealtimeConnectionAsync(CreateRealtimeConnectionCommand command, CancellationToken cancellationToken);

    Task<ConnectRealtimeWebSocketResponse> ConnectRealTimeWebSocketAsync(ConnectRealtimeWebSocketCommand command, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<CreateRealtimeConnectionResponse> CreateRealtimeConnectionAsync(CreateRealtimeConnectionCommand command, CancellationToken cancellationToken)
    {
        var (_, session) = await InitialRealtimeSessionsAsync(command.AssistantId, command.CustomPrompt, cancellationToken).ConfigureAwait(false);

        var ephemeralToken = await _openaiClient.InitialRealtimeSessionsAsync(session, cancellationToken).ConfigureAwait(false);
        
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

    public async Task<ConnectRealtimeWebSocketResponse> ConnectRealTimeWebSocketAsync(ConnectRealtimeWebSocketCommand command, CancellationToken cancellationToken)
    {
        var (assistant, session) = await InitialRealtimeSessionsAsync(command.AssistantId, command.CustomPrompt, cancellationToken).ConfigureAwait(false);
        
        ConfigWebSocketRequestHeader(assistant);

        var url = string.IsNullOrEmpty(assistant?.ModelUrl) ? AiSpeechAssistantStore.DefaultUrl : assistant.ModelUrl;
        
        await _openaiClientWebSocket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        
        await SendToWebSocketAsync(_openaiClientWebSocket, new { type = "session.update", session = session }, cancellationToken).ConfigureAwait(false);
        
        return new ConnectRealtimeWebSocketResponse { Data = session };
    }

    private async Task<(Domain.AISpeechAssistant.AiSpeechAssistant Assistant, OpenAiRealtimeSessionDto Session)> InitialRealtimeSessionsAsync(int? assistantId, string customPrompt, CancellationToken cancellationToken = default)
    {
        var prompt = await GenerateFinalPromptAsync(assistantId, customPrompt, cancellationToken).ConfigureAwait(false);

        var assistant = assistantId.HasValue
            ? await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantAsync(assistantId.Value, cancellationToken).ConfigureAwait(false)
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

        return (assistant, session);
    }

    private async Task<string> GenerateFinalPromptAsync(int? assistantId, string customPrompt, CancellationToken cancellationToken = default)
    {
        var prompt = string.IsNullOrWhiteSpace(customPrompt) ? "You are a friendly assistant" : customPrompt;

        if (!assistantId.HasValue) return prompt;
        
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
            assistantId: assistantId.Value, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (knowledge == null) 
            throw new Exception($"Could not found the knowledge by id: {assistantId.Value}");

        if (!string.IsNullOrWhiteSpace(customPrompt) && !string.IsNullOrWhiteSpace(knowledge.Prompt))
            prompt += $"\n\n{knowledge.Prompt}";
        else
            prompt = knowledge.Prompt;

        return prompt;
    }
}