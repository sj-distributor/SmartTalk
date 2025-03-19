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

    Task<SwitchAiSpeechAssistantKnowledgeVersionResponse> SwitchAiSpeechAssistantKnowledgeVersionAsync(SwitchAiSpeechAssistantKnowledgeVersionCommand command, CancellationToken cancellationToken);

    Task<UpdateAiSpeechAssistantResponse> UpdateAiSpeechAssistantAsync(UpdateAiSpeechAssistantCommand command, CancellationToken cancellationToken);
    
    Task<DeleteAiSpeechAssistantResponse> DeleteAiSpeechAssistantAsync(DeleteAiSpeechAssistantCommand command, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<AddAiSpeechAssistantResponse> AddAiSpeechAssistantAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var assistant = InitialAiSpeechAssistant(command);

        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);

        await UpdateNumbersStatusAsync([command.AnsweringNumberId], true, cancellationToken).ConfigureAwait(false);

        return new AddAiSpeechAssistantResponse
        {
            Data = _mapper.Map<AiSpeechAssistantDto>(assistant)
        };
    }

    public async Task<AddAiSpeechAssistantKnowledgeResponse> AddAiSpeechAssistantKnowledgeAsync(AddAiSpeechAssistantKnowledgeCommand command, CancellationToken cancellationToken)
    {
        var preKnowledge = await UpdatePreviousKnowledgeIfRequiredAsync(command.AssistantId, false, cancellationToken).ConfigureAwait(false);

        var latestKnowledge = _mapper.Map<AiSpeechAssistantKnowledge>(command);

        InitialKnowledge(preKnowledge, latestKnowledge);

        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgesAsync([latestKnowledge], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new AddAiSpeechAssistantKnowledgeResponse
        {
            Data = _mapper.Map<AiSpeechAssistantKnowledgeDto>(latestKnowledge)
        };
    }

    public async Task<SwitchAiSpeechAssistantKnowledgeVersionResponse> SwitchAiSpeechAssistantKnowledgeVersionAsync(SwitchAiSpeechAssistantKnowledgeVersionCommand command, CancellationToken cancellationToken)
    {
        var preKnowledge = await UpdatePreviousKnowledgeIfRequiredAsync(command.AssistantId, false, cancellationToken).ConfigureAwait(false);

        if (preKnowledge == null) throw new Exception("Could not found the active knowledge!");

        var currentKnowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
            command.AssistantId, command.KnowledgeId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (currentKnowledge == null) throw new Exception($"Could not found the knowledge by knowledge id: {command.KnowledgeId}!");

        await UpdateKnowledgeStatusAsync(currentKnowledge, true, cancellationToken).ConfigureAwait(false);

        return new SwitchAiSpeechAssistantKnowledgeVersionResponse
        {
            Data = _mapper.Map<AiSpeechAssistantKnowledgeDto>(currentKnowledge)
        };
    }

    public async Task<UpdateAiSpeechAssistantResponse> UpdateAiSpeechAssistantAsync(UpdateAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantAsync(command.AssistantId, cancellationToken).ConfigureAwait(false);

        await UpdateAssistantNumberIfRequiredAsync(assistant.AnsweringNumberId, command.AnsweringNumberId, cancellationToken).ConfigureAwait(false);
        
        _mapper.Map(command, assistant);

        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdateAiSpeechAssistantResponse
        {
            Data = _mapper.Map<AiSpeechAssistantDto>(assistant)
        };
    }

    public async Task<DeleteAiSpeechAssistantResponse> DeleteAiSpeechAssistantAsync(DeleteAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var assistants = await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantsAsync(
                command.AssistantIds, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (assistants.Count == 0) throw new Exception("Delete assistants failed.");

        await UpdateNumbersStatusAsync(assistants.Select(x => x.AnsweringNumberId).ToList(), false, cancellationToken).ConfigureAwait(false);

        return new DeleteAiSpeechAssistantResponse
        {
            Data = _mapper.Map<List<AiSpeechAssistantDto>>(assistants)
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

    private async Task<AiSpeechAssistantKnowledge> UpdatePreviousKnowledgeIfRequiredAsync(int assistantId, bool isActive, CancellationToken cancellationToken)
    {
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(assistantId, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (knowledge == null)
        {
            Log.Information("Could not found the previous knowledge.");

            return null;
        }

        await UpdateKnowledgeStatusAsync(knowledge, isActive, cancellationToken).ConfigureAwait(false);
            
        return knowledge;
    }

    private async Task UpdateKnowledgeStatusAsync(AiSpeechAssistantKnowledge knowledge, bool isActive, CancellationToken cancellationToken)
    {
        knowledge.IsActive = isActive;

        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync([knowledge], cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private async Task UpdateNumbersStatusAsync(List<int> answeringNumberIds, bool isUsed, CancellationToken cancellationToken)
    {
        var numbers = await _aiSpeechAssistantDataProvider.GetNumbersAsync(answeringNumberIds, cancellationToken).ConfigureAwait(false);

        numbers.ForEach(x => x.IsUsed = isUsed);

        await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync(numbers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAssistantNumberIfRequiredAsync(int preNumberId, int currentNumberId, CancellationToken cancellationToken)
    {
        if (preNumberId == currentNumberId) return;

        var numbers = await _aiSpeechAssistantDataProvider.GetNumbersAsync([preNumberId, currentNumberId], cancellationToken).ConfigureAwait(false);

        var updateNumbers = numbers.Where(number =>
        {
            if (number != null && number.Id == preNumberId)
            {
                number.IsUsed = false;
                return true;
            }
            
            if (number != null && number.Id == currentNumberId)
            {
                number.IsUsed = true;
                return true;
            }

            return false;
        }).ToList();
        
        await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync(updateNumbers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}