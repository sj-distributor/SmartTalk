using Serilog;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Agent;
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
        var assistant = await InitialAiSpeechAssistantAsync(command, cancellationToken).ConfigureAwait(false);

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

        await UpdateNumbersStatusAsync(assistants.Where(x => x.AnsweringNumberId.HasValue).Select(x => x.AnsweringNumberId.Value).ToList(), false, cancellationToken).ConfigureAwait(false);

        return new DeleteAiSpeechAssistantResponse
        {
            Data = _mapper.Map<List<AiSpeechAssistantDto>>(assistants)
        };
    }

    private async Task<Domain.AISpeechAssistant.AiSpeechAssistant> InitialAiSpeechAssistantAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var (agent, number) = await InitialAssistantRelatedInfoAsync(command.AssistantName, cancellationToken).ConfigureAwait(false);
        
        var assistant = new Domain.AISpeechAssistant.AiSpeechAssistant
        {
            AgentId = agent.Id,
            ModelVoice = "alloy",
            Name = command.AssistantName,
            AnsweringNumberId = number?.Id,
            AnsweringNumber = number?.Number,
            CreatedBy = _currentUser.Id.Value,
            ModelUrl = AiSpeechAssistantStore.DefaultUrl,
            ModelProvider = AiSpeechAssistantProvider.OpenAi
        };
        
        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await InitialAssistantKnowledgeAsync(command, assistant, cancellationToken).ConfigureAwait(false);

        return assistant;
    }
    
    private async Task<(Agent agent, NumberPool number)> InitialAssistantRelatedInfoAsync(string assistantName, CancellationToken cancellationToken)
    {
        var domain = new Restaurant { Name = assistantName };

        await _restaurantDataProvider.AddRestaurantAsync(domain, cancellationToken: cancellationToken).ConfigureAwait(false);

        var agent = new Agent
        {
            RelateId = domain.Id,
            Type = AgentType.Restaurant,
            SourceSystem = AgentSourceSystem.Self
        };

        await _agentDataProvider.AddAgentAsync(agent, cancellationToken: cancellationToken).ConfigureAwait(false);

        var number = await _aiSpeechAssistantDataProvider.GetNumberAsync(isUsed: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return (agent, number);
    }

    private async Task InitialAssistantKnowledgeAsync(AddAiSpeechAssistantCommand command, Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken)
    {
        var knowledge = new AiSpeechAssistantKnowledge
        {
            Version = "1.0",
            IsActive = true,
            Json = command.Json,
            AssistantId = assistant.Id,
            Greetings = command.Greetings,
            CreatedBy = _currentUser.Id.Value
        };

        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgesAsync([knowledge], cancellationToken: cancellationToken).ConfigureAwait(false);
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
        if (answeringNumberIds.Count == 0) return;
        
        var numbers = await _aiSpeechAssistantDataProvider.GetNumbersAsync(answeringNumberIds, cancellationToken).ConfigureAwait(false);

        numbers.ForEach(x => x.IsUsed = isUsed);

        await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync(numbers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAssistantNumberIfRequiredAsync(int? preNumberId, int? currentNumberId, CancellationToken cancellationToken)
    {
        if (preNumberId == currentNumberId) return;

        var numberIds = new List<int>();
        if (preNumberId.HasValue) numberIds.Add(preNumberId.Value);
        if (currentNumberId.HasValue) numberIds.Add(currentNumberId.Value);

        if (numberIds.Count == 0) return;

        var numbers = await _aiSpeechAssistantDataProvider.GetNumbersAsync(numberIds, cancellationToken).ConfigureAwait(false);
    
        if (numbers.Count == 0) return;

        foreach (var number in numbers)
        {
            if (number.Id == preNumberId) number.IsUsed = false;
            if (number.Id == currentNumberId) number.IsUsed = true;
        }

        await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync(numbers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}