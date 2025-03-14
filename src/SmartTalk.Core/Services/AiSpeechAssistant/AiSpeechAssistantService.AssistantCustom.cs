using Serilog;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<AddAiSpeechAssistantResponse> AddAiSpeechAssistantAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken);

    Task<AddAiSpeechAssistantKnowledgeResponse> AddAiSpeechAssistantKnowledgeAsync(AddAiSpeechAssistantKnowledgeCommand command, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<AddAiSpeechAssistantResponse> AddAiSpeechAssistantAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var assistant = InitialAiSpeechAssistant(command);

        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);

        await UpdateNumberStatusAsync(command.AnsweringNumberId, cancellationToken).ConfigureAwait(false);

        return new AddAiSpeechAssistantResponse
        {
            Data = _mapper.Map<AiSpeechAssistantDto>(assistant)
        };
    }

    public async Task<AddAiSpeechAssistantKnowledgeResponse> AddAiSpeechAssistantKnowledgeAsync(AddAiSpeechAssistantKnowledgeCommand command, CancellationToken cancellationToken)
    {
        var preKnowledge = await UpdatePreviousKnowledgeIfRequiredAsync(command.AssistantId, cancellationToken).ConfigureAwait(false);

        var latestKnowledge = _mapper.Map<AiSpeechAssistantKnowledge>(command);

        InitialKnowledge(preKnowledge, latestKnowledge);

        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgesAsync([latestKnowledge], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new AddAiSpeechAssistantKnowledgeResponse
        {
            Data = _mapper.Map<AiSpeechAssistantKnowledgeDto>(latestKnowledge)
        };
    }

    private Domain.AISpeechAssistant.AiSpeechAssistant InitialAiSpeechAssistant(AddAiSpeechAssistantCommand command)
    {
        var assistant = _mapper.Map<Domain.AISpeechAssistant.AiSpeechAssistant>(command);

        assistant.ModelVoice = "alloy";
        assistant.CreatedBy = _currentUser.Id.Value;
        assistant.ModelUrl = AiSpeechAssistantStore.DefaultUrl;
        assistant.ModelProvider = AiSpeechAssistantProvider.OpenAi;
        
        return assistant;
    }

    private async Task<AiSpeechAssistantKnowledge> UpdatePreviousKnowledgeIfRequiredAsync(int assistantId, CancellationToken cancellationToken)
    {
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(assistantId, true, cancellationToken).ConfigureAwait(false);

        if (knowledge == null)
        {
            Log.Information("Could not found the previous knowledge.");

            return null;
        }

        knowledge.IsActive = false;

        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync([knowledge], cancellationToken: cancellationToken).ConfigureAwait(false);
            
        return knowledge;
    }

    private void InitialKnowledge(AiSpeechAssistantKnowledge preKnowledge, AiSpeechAssistantKnowledge latestKnowledge)
    {
        latestKnowledge.IsActive = true;
        latestKnowledge.CreatedBy = _currentUser.Id.Value;
        
        if (preKnowledge == null)
        {
            latestKnowledge.Version = "1.0";
            
            return;
        }

        latestKnowledge.Version = (double.Parse(preKnowledge.Version) + 0.1).ToString("F1");
    }

    private async Task UpdateNumberStatusAsync(int answeringNumberId, CancellationToken cancellationToken)
    {
        var number =await _aiSpeechAssistantDataProvider.GetNumberAsync(answeringNumberId, cancellationToken).ConfigureAwait(false);

        number.IsUsed = true;

        await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync([number], cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}