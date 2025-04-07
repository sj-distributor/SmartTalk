using Serilog;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<CreateRealtimeConnectionResponse> CreateRealtimeConnectionAsync(CreateRealtimeConnectionCommand command, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<CreateRealtimeConnectionResponse> CreateRealtimeConnectionAsync(CreateRealtimeConnectionCommand command, CancellationToken cancellationToken)
    {
        var prompt = await GenerateFinalPromptAsync(command, cancellationToken).ConfigureAwait(false);

        var ephemeralToken = await _openaiClient.InitialRealtimeSessionsAsync(prompt, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(ephemeralToken)) throw new Exception("Invalid ephemeral token");
        
        var sdpAnswer = await _openaiClient.RealtimeChatAsync(command.OfferSdp, ephemeralToken, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Create realtime connection response: {@Response}" , sdpAnswer);

        return new CreateRealtimeConnectionResponse
        {
            Data = sdpAnswer
        };
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