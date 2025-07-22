using Serilog;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Globalization;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Events.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<AddAiSpeechAssistantResponse> AddAiSpeechAssistantAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken);

    Task<AiSpeechAssistantKnowledgeAddedEvent> AddAiSpeechAssistantKnowledgeAsync(AddAiSpeechAssistantKnowledgeCommand command, CancellationToken cancellationToken);

    Task<SwitchAiSpeechAssistantKnowledgeVersionResponse> SwitchAiSpeechAssistantKnowledgeVersionAsync(SwitchAiSpeechAssistantKnowledgeVersionCommand command, CancellationToken cancellationToken);

    Task<UpdateAiSpeechAssistantResponse> UpdateAiSpeechAssistantAsync(UpdateAiSpeechAssistantCommand command, CancellationToken cancellationToken);
    
    Task<DeleteAiSpeechAssistantResponse> DeleteAiSpeechAssistantAsync(DeleteAiSpeechAssistantCommand command, CancellationToken cancellationToken);

    Task<UpdateAiSpeechAssistantNumberResponse> UpdateAiSpeechAssistantNumberAsync(UpdateAiSpeechAssistantNumberCommand command, CancellationToken cancellationToken);
    
    Task<UpdateAiSpeechAssistantKnowledgeResponse> UpdateAiSpeechAssistantKnowledgeAsync(UpdateAiSpeechAssistantKnowledgeCommand command, CancellationToken cancellationToken);
    
    Task<UpdateAiSpeechAssistantSessionResponse> UpdateAiSpeechAssistantSessionAsync(UpdateAiSpeechAssistantSessionCommand command, CancellationToken cancellationToken);
    
    Task<AddAiSpeechAssistantSessionResponse> AddAiSpeechAssistantSessionAsync(AddAiSpeechAssistantSessionCommand command, CancellationToken cancellationToken);
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

    public async Task<AiSpeechAssistantKnowledgeAddedEvent> AddAiSpeechAssistantKnowledgeAsync(AddAiSpeechAssistantKnowledgeCommand command, CancellationToken cancellationToken)
    {
        var prevKnowledge = await UpdatePreviousKnowledgeIfRequiredAsync(command.AssistantId, false, cancellationToken).ConfigureAwait(false);

        var latestKnowledge = _mapper.Map<AiSpeechAssistantKnowledge>(command);

        await InitialKnowledgeAsync(latestKnowledge, cancellationToken).ConfigureAwait(false);

        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgesAsync([latestKnowledge], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new AiSpeechAssistantKnowledgeAddedEvent
        {
            PrevKnowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(prevKnowledge),
            LatestKnowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(latestKnowledge)
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
        assistant.Channel = command.Channels == null ? null : string.Join(",", command.Channels.Select(x => (int)x));

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

        await DeleteAssistantRelatedInfoAsync(assistants.Select(x => x.AgentId).ToList(), cancellationToken).ConfigureAwait(false);
        
        return new DeleteAiSpeechAssistantResponse
        {
            Data = _mapper.Map<List<AiSpeechAssistantDto>>(assistants)
        };
    }

    public async Task<UpdateAiSpeechAssistantNumberResponse> UpdateAiSpeechAssistantNumberAsync(UpdateAiSpeechAssistantNumberCommand command, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantAsync(command.AssistantId, cancellationToken).ConfigureAwait(false);

        var number = await AssistantNumberSwitchAsync(assistant, cancellationToken).ConfigureAwait(false);
        
        return new UpdateAiSpeechAssistantNumberResponse
        {
            Data = _mapper.Map<NumberPoolDto>(number)
        };
    }

    public async Task<UpdateAiSpeechAssistantKnowledgeResponse> UpdateAiSpeechAssistantKnowledgeAsync(UpdateAiSpeechAssistantKnowledgeCommand command, CancellationToken cancellationToken)
    {
        var knowledgeId = command.KnowledgeId ?? (command.AssistantId.HasValue
            ? (await GetAiSpeechAssistantKnowledgeAsync(command.AssistantId.Value, cancellationToken).ConfigureAwait(false))?.Id : null);
        
        Log.Information("Initial knowledge id if required, original:{OriginalKnowledgeId}, {knowledgeId}, {AssistantId}", command.KnowledgeId, knowledgeId, command.AssistantId);

        if (knowledgeId == null)
            throw new Exception("Could not find the knowledge id!");
        
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(knowledgeId: knowledgeId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (knowledge == null) throw new Exception("Could not found the knowledge by knowledge id");

        if(!string.IsNullOrWhiteSpace(command.Brief))
            knowledge.Brief = command.Brief;
        
        if (!string.IsNullOrWhiteSpace(command.Greetings))
            knowledge.Greetings = command.Greetings;

        if (command.VoiceType.HasValue)
            await UpdateAssistantVoiceIfRequiredAsync(knowledge.AssistantId, command.ModelVoice, command.VoiceType.Value, command.MediaType, cancellationToken).ConfigureAwait(false);
        
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync([knowledge], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new UpdateAiSpeechAssistantKnowledgeResponse
        {
            Data = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge),
        };
    }
    
    public async Task<AddAiSpeechAssistantSessionResponse> AddAiSpeechAssistantSessionAsync(AddAiSpeechAssistantSessionCommand command, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();

        var session = new AiSpeechAssistantSession
        {
            AssistantId = command.AssistantId,
            SessionId = sessionId,
            Count = 0
        };
        
        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantSessionAsync(session, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AddAiSpeechAssistantSessionResponse { Data = sessionId };
    }

    public async Task<UpdateAiSpeechAssistantSessionResponse> UpdateAiSpeechAssistantSessionAsync(UpdateAiSpeechAssistantSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantSessionBySessionIdAsync(command.SessionId, cancellationToken).ConfigureAwait(false);

        if (session == null) throw new Exception($"Could not found the session by session id: {command.SessionId}!");
        
        await _redisSafeRunner.ExecuteWithLockAsync($"session-update-{command.SessionId}", async () =>
        {
            session.Count++;
            
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantSessionAsync(session, true, cancellationToken).ConfigureAwait(false);
        }, wait: TimeSpan.FromSeconds(10), retry: TimeSpan.FromSeconds(1), server: RedisServer.System).ConfigureAwait(false);
        
        return new UpdateAiSpeechAssistantSessionResponse
        {
            Data = _mapper.Map<AiSpeechAssistantSessionDto>(session)
        };
    }

    private async Task UpdateAssistantVoiceIfRequiredAsync(int assistantId, string modelVoice, AiKidVoiceType? voiceType, AiSpeechAssistantMediaType? mediaType, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantAsync(assistantId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get assistant for update model voice: {@Assistant}", assistant);

        if (assistant != null)
        {
            assistant.ModelVoice = ModelVoiceMapping(modelVoice, voiceType, mediaType);
                
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<AiSpeechAssistantKnowledge> GetAiSpeechAssistantKnowledgeAsync(int assistantId, CancellationToken cancellationToken)
    {
        var knowledge = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantKnowledgeAsync(assistantId: assistantId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get knowledge by assistant when update the knowledge: {@Knowledge}", knowledge);
        
        return knowledge;
    }

    private async Task<Domain.AISpeechAssistant.AiSpeechAssistant> InitialAiSpeechAssistantAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var (agent, number) = await InitialAssistantRelatedInfoAsync(command , cancellationToken).ConfigureAwait(false);
        
        var assistant = new Domain.AISpeechAssistant.AiSpeechAssistant
        {
            AgentId = agent.Id,
            ModelVoice = ModelVoiceMapping(command.ModelVoice, command.VoiceType, command.MediaType),
            Name = command.AssistantName,
            AnsweringNumberId = number?.Id,
            AnsweringNumber = number?.Number,
            CreatedBy = _currentUser.Id.Value,
            ModelUrl = GetDefaultModuleUrl(command.ModelUrl, command.ModelProvider, command.AgentType),
            ModelProvider = command.ModelProvider,
            ModelName = command.ModelName,
            ModelLanguage = command.ModelLanguage,
            Channel = command.Channels == null ? null : string.Join(",", command.Channels.Select(x => (int)x)),
            IsDisplay = command.IsDisplay
        };
        
        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await InitialAssistantKnowledgeAsync(command, assistant, cancellationToken).ConfigureAwait(false);

        return assistant;
    }

    private string GetDefaultModuleUrl(string modelUrl, AiSpeechAssistantProvider provider, AgentType type)
    {
        if (!string.IsNullOrEmpty(modelUrl)) return modelUrl;

        return type switch
        {
            AgentType.AiKid => provider == AiSpeechAssistantProvider.OpenAi ? AiSpeechAssistantStore.AiKidDefaultUrl : AiSpeechAssistantStore.GoogleDefaultUrl,
            _ => provider == AiSpeechAssistantProvider.OpenAi ? AiSpeechAssistantStore.DefaultUrl : AiSpeechAssistantStore.GoogleDefaultUrl
        };
    }

    private string ModelVoiceMapping(string modelVoice, AiKidVoiceType? voiceType, AiSpeechAssistantMediaType? mediaType)
    {
        if (!string.IsNullOrEmpty(modelVoice)) return modelVoice;
        
        if (!voiceType.HasValue) return "alloy";
        
        return voiceType.Value switch
        {
            AiKidVoiceType.Male => mediaType.HasValue ? (mediaType == AiSpeechAssistantMediaType.Audio ? "ash" : "Puck") : "ash",
            _ => mediaType.HasValue ? (mediaType == AiSpeechAssistantMediaType.Audio ? "alloy" : "Aoede") : "alloy"
        };
    }
    
    private async Task<(Agent agent, NumberPool number)> InitialAssistantRelatedInfoAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        Restaurant domain = null;
        
        if (command.AgentType != AgentType.AiKid)
        {
            domain = new Restaurant { Name = command.AssistantName };

            await _restaurantDataProvider.AddRestaurantAsync(domain, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var agent = new Agent
        {
            RelateId = domain?.Id ?? 0,
            Type = command.AgentType,
            SourceSystem = command.SourceSystem
        };

        await _agentDataProvider.AddAgentAsync(agent, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (command.AgentType == AgentType.AiKid && command.Uuid.HasValue)
            await _aiSpeechAssistantDataProvider.AddAiKidAsync(new AiKid
            {
                AgentId = agent.Id,
                KidUuid = command.Uuid.Value
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!command.IsDisplay) return (agent, null);
        
        var number = await _aiSpeechAssistantDataProvider.GetNumberAsync(isUsed: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (number != null)
        {
            number.IsUsed = true;
            await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync([number], cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        
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
            CreatedBy = _currentUser.Id.Value,
            Prompt = GenerateKnowledgePrompt(command.Json)
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

    private async Task InitialKnowledgeAsync(AiSpeechAssistantKnowledge latestKnowledge, CancellationToken cancellationToken)
    {
        latestKnowledge.IsActive = true;
        latestKnowledge.CreatedBy = _currentUser.Id.Value;
        latestKnowledge.Prompt = GenerateKnowledgePrompt(latestKnowledge.Json);
        latestKnowledge.Version = await HandleKnowledgeVersionAsync(latestKnowledge, cancellationToken).ConfigureAwait(false);
    }

    private string GenerateKnowledgePrompt(string json)
    {
        var prompt = new StringBuilder();
        var jsonData = JObject.Parse(json);
        var textInfo = CultureInfo.InvariantCulture.TextInfo;

        foreach (var property in jsonData.Properties())
        {
            var key = textInfo.ToTitleCase(property.Name);
            var value = property.Value;

            if (value is JArray array)
            {
                var list = array.Select((item, index) => $"{index + 1}. {item.ToString()}").ToList();
                prompt.AppendLine($"{key}：\n{string.Join("\n", list)}\n");
            }
            else
                prompt.AppendLine($"{key}： {value}\n");
        }

        return prompt.ToString();
    }

    private async Task<string> HandleKnowledgeVersionAsync(AiSpeechAssistantKnowledge latestKnowledge, CancellationToken cancellationToken)
    {
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeOrderByVersionAsync(latestKnowledge.AssistantId, cancellationToken).ConfigureAwait(false);
        
        if (knowledge == null) return "1.0";

        var versionParts = knowledge.Version.Split('.');
        if (versionParts.Length != 2 || !int.TryParse(versionParts[0], out var majorVersion) || !int.TryParse(versionParts[1], out var minorVersion))
        {
            return "1.0";
        }
        
        if (minorVersion == 9)
        {
            majorVersion++;
            minorVersion = 0;
        }
        else
            minorVersion++;
        
        return $"{majorVersion}.{minorVersion}";
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

    private async Task<NumberPool> AssistantNumberSwitchAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken)
    {
        var number = assistant.AnsweringNumberId.HasValue
            ? await _aiSpeechAssistantDataProvider.GetNumberAsync(assistant.AnsweringNumberId.Value, cancellationToken: cancellationToken).ConfigureAwait(false)
            : await _aiSpeechAssistantDataProvider.GetNumberAsync(isUsed: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (assistant.AnsweringNumberId.HasValue)
        {
            if (number == null) throw new Exception("Could not found the number");
            
            number.IsUsed = false;
            await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync([number], cancellationToken: cancellationToken).ConfigureAwait(false);

            assistant.AnsweringNumberId = null;
            assistant.AnsweringNumber = null;
        }
        else
        {
            if (number == null) throw new Exception("No available numbers.");
            
            number.IsUsed = true;
            await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync([number], cancellationToken: cancellationToken).ConfigureAwait(false);
            
            assistant.AnsweringNumberId = number.Id;
            assistant.AnsweringNumber = number.Number;
        }
        
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return number;
    }
    
    private async Task DeleteAssistantRelatedInfoAsync(List<int> agentIds, CancellationToken cancellationToken)
    {
        var agents = await _agentDataProvider.GetAgentsAsync(agentIds: agentIds, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (agents.Count == 0) return;

        var domains = await _restaurantDataProvider.GetRestaurantsAsync(agents.Select(x => x.RelateId).ToList(), cancellationToken).ConfigureAwait(false);

        await _agentDataProvider.DeleteAgentsAsync(agents, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (domains.Count == 0) return;

        await _restaurantDataProvider.DeleteRestaurantsAsync(domains, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}