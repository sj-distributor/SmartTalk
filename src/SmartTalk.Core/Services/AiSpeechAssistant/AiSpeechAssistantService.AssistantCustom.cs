using Serilog;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Newtonsoft.Json;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeCopy;
using SmartTalk.Core.Domain.Pos;
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
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<AddAiSpeechAssistantResponse> AddAiSpeechAssistantAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken);

    Task<AiSpeechAssistantKnowledgeAddedEvent> AddAiSpeechAssistantKnowledgeAsync(AddAiSpeechAssistantKnowledgeCommand command, CancellationToken cancellationToken);

    Task<SwitchAiSpeechAssistantKnowledgeVersionResponse> SwitchAiSpeechAssistantKnowledgeVersionAsync(SwitchAiSpeechAssistantKnowledgeVersionCommand command, CancellationToken cancellationToken);

    Task<UpdateAiSpeechAssistantResponse> UpdateAiSpeechAssistantAsync(UpdateAiSpeechAssistantCommand command, CancellationToken cancellationToken);
    
    Task<UpdateAiSpeechAssistantDetailResponse> UpdateAiSpeechAssistantDetailAsync(UpdateAiSpeechAssistantDetailCommand command, CancellationToken cancellationToken);
    
    Task<DeleteAiSpeechAssistantResponse> DeleteAiSpeechAssistantAsync(DeleteAiSpeechAssistantCommand command, CancellationToken cancellationToken);

    Task<UpdateAiSpeechAssistantNumberResponse> UpdateAiSpeechAssistantNumberAsync(UpdateAiSpeechAssistantNumberCommand command, CancellationToken cancellationToken);
    
    Task<UpdateAiSpeechAssistantKnowledgeResponse> UpdateAiSpeechAssistantKnowledgeAsync(UpdateAiSpeechAssistantKnowledgeCommand command, CancellationToken cancellationToken);
    
    Task<UpdateAiSpeechAssistantSessionResponse> UpdateAiSpeechAssistantSessionAsync(UpdateAiSpeechAssistantSessionCommand command, CancellationToken cancellationToken);
    
    Task<AddAiSpeechAssistantSessionResponse> AddAiSpeechAssistantSessionAsync(AddAiSpeechAssistantSessionCommand command, CancellationToken cancellationToken);
    
    Task<SwitchAiSpeechDefaultAssistantResponse> SwitchAiSpeechDefaultAssistantAsync(SwitchAiSpeechDefaultAssistantCommand command, CancellationToken cancellationToken);
    
    Task<AddAiSpeechAssistantInboundRoutesResponse> AddAiSpeechAssistantInboundRoutesAsync(AddAiSpeechAssistantInboundRoutesCommand command, CancellationToken cancellationToken);
    
    Task<UpdateAiSpeechAssistantInboundRouteResponse> UpdateAiSpeechAssistantInboundRouteAsync(UpdateAiSpeechAssistantInboundRouteCommand command, CancellationToken cancellationToken);
    
    Task<DeleteAiSpeechAssistantInboundRoutesResponse> DeleteAiSpeechAssistantInboundRoutesAsync(DeleteAiSpeechAssistantInboundRoutesCommand command, CancellationToken cancellationToken);

    Task<KonwledgeCopyAddedEvent> KonwledgeCopyAsync(KonwledgeCopyCommand command, CancellationToken cancellationToken);

    Task<GetKonwledgesResponse> GetKonwledgesAsync(GetKonwledgesRequest request, CancellationToken cancellationToken);
    
    Task<GetKonwledgeRelatedResponse> GetKonwledgeRelatedAsync(GetKonwledgeRelatedRequest request, CancellationToken cancellationToken);

    Task SyncCopiedKnowledgesIfRequiredAsync(List<AiSpeechAssistantKnowledge> sourceKnowledges, bool deleteKnowledge, CancellationToken cancellationToken);
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

        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgesAsync([latestKnowledge], true, cancellationToken).ConfigureAwait(false);
       
        var allPrevRelateds = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedBySourceKnowledgeIdAsync([prevKnowledge.Id], cancellationToken).ConfigureAwait(false);
        var relatedDtoMap = command.RelatedKnowledges.ToDictionary(x => x.Id, x => x);
        
        var selectedRelateds = allPrevRelateds.Where(r => relatedDtoMap.ContainsKey(r.Id)).ToList();
        
        allPrevRelateds = allPrevRelateds
            .Select(r =>
            {
                r.TargetKnowledgeId = latestKnowledge.Id;

                if (relatedDtoMap.TryGetValue(r.Id, out var dto))
                { r.CopyKnowledgePoints = dto.CopyKnowledgePoints; } return r; 
            }).ToList();
        
        if (allPrevRelateds.Any())
        {
            await _aiSpeechAssistantDataProvider.UpdateKnowledgeCopyRelatedAsync(allPrevRelateds, true, cancellationToken).ConfigureAwait(false);
        }
        
        await InitialKnowledgeAsync(latestKnowledge, selectedRelateds, cancellationToken).ConfigureAwait(false);
        
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync([latestKnowledge], true, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(command.Language))
        {
            var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(command.AssistantId, cancellationToken).ConfigureAwait(false);

            assistant.ModelLanguage = command.Language;

            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([assistant], true, cancellationToken).ConfigureAwait(false);
        }

        var prevKnowledgeDto = _mapper.Map<AiSpeechAssistantKnowledgeDto>(prevKnowledge);
        var latestKnowledgeDto = _mapper.Map<AiSpeechAssistantKnowledgeDto>(latestKnowledge);

        prevKnowledgeDto.KnowledgeCopyRelateds = _mapper.Map<List<KnowledgeCopyRelatedDto>>(prevKnowledge.KnowledgeCopyRelateds);
        latestKnowledgeDto.KnowledgeCopyRelateds = _mapper.Map<List<KnowledgeCopyRelatedDto>>(selectedRelateds);

        return new AiSpeechAssistantKnowledgeAddedEvent
        {
            PrevKnowledge = prevKnowledgeDto,
            LatestKnowledge = latestKnowledgeDto
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
        var storeUser = await _posDataProvider.GetPosStoreUsersByUserIdAndAssistantIdAsync([command.AssistantId], _currentUser.Id.Value, cancellationToken).ConfigureAwait(false);

        if (storeUser == null)
            throw new Exception("Do not have the store permission to operate");
        
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantAsync(command.AssistantId, cancellationToken).ConfigureAwait(false);
        
        await UpdateAssistantNumberIfRequiredAsync(assistant.AnsweringNumberId, command.AnsweringNumberId, cancellationToken).ConfigureAwait(false);
        
        _mapper.Map(command, assistant);
        assistant.Channel = command.Channels == null ? null : string.Join(",", command.Channels.Select(x => (int)x));

        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);

        await UpdateAiSpeechAssistantConfigsAsync(assistant, command.TransferCallNumber, cancellationToken).ConfigureAwait(false);
        
        return new UpdateAiSpeechAssistantResponse
        {
            Data = _mapper.Map<AiSpeechAssistantDto>(assistant)
        };
    }

    public async Task<UpdateAiSpeechAssistantDetailResponse> UpdateAiSpeechAssistantDetailAsync(UpdateAiSpeechAssistantDetailCommand command, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(command.AssistantId, cancellationToken).ConfigureAwait(false);
        
        _mapper.Map(command, assistant);
        
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new UpdateAiSpeechAssistantDetailResponse { Data = _mapper.Map<AiSpeechAssistantDto>(assistant) };
    }

    public async Task<DeleteAiSpeechAssistantResponse> DeleteAiSpeechAssistantAsync(DeleteAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var storeUser = await _posDataProvider.GetPosStoreUsersByUserIdAndAssistantIdAsync(command.AssistantIds, _currentUser.Id.Value, cancellationToken).ConfigureAwait(false);

        if (storeUser == null)
            throw new Exception("Do not have the store permission to operate");
        
        var assistants = await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantByIdsAsync(command.AssistantIds, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (assistants.Count == 0) throw new Exception("Delete assistants failed.");

        await UpdateNumbersStatusAsync(assistants.Where(x => x.AnsweringNumberId.HasValue).Select(x => x.AnsweringNumberId.Value).ToList(), false, cancellationToken).ConfigureAwait(false);
        
        var agents = await DeleteAssistantRelatedInfoAsync(assistants.Select(x => x.Id).ToList(), command.IsDeleteAgent, cancellationToken).ConfigureAwait(false);

        await _posDataProvider.DeletePosAgentsByAgentIdsAsync(agents.Select(x => x.Id).ToList(), true, cancellationToken).ConfigureAwait(false);
        
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
            await UpdateAssistantVoiceIfRequiredAsync(knowledge.AssistantId, command.VoiceType.Value, cancellationToken).ConfigureAwait(false);
        
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

    public async Task<SwitchAiSpeechDefaultAssistantResponse> SwitchAiSpeechDefaultAssistantAsync(SwitchAiSpeechDefaultAssistantCommand command, CancellationToken cancellationToken)
    {
        var assistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdsAsync([command.PreviousAssistantId, command.LatestAssistantId], cancellationToken).ConfigureAwait(false);

        var previousDefaultAssistant = assistants.Where(x => x.Id == command.PreviousAssistantId).FirstOrDefault();
        var latestDefaultAssistant = assistants.Where(x => x.Id == command.LatestAssistantId).FirstOrDefault();
        
        Log.Information("Get the default assistant, previous: {@PreviousDefaultAssistant}, latest: {@LatestDefaultAssistant}", previousDefaultAssistant, latestDefaultAssistant);

        if (previousDefaultAssistant == null || latestDefaultAssistant == null)
            throw new Exception("Could not find the previous or latest default assistant!");

        previousDefaultAssistant.IsDefault = false;
        latestDefaultAssistant.IsDefault = true;
        latestDefaultAssistant.AnsweringNumber = previousDefaultAssistant.AnsweringNumber;
        latestDefaultAssistant.AnsweringNumberId = previousDefaultAssistant.AnsweringNumberId;
        previousDefaultAssistant.AnsweringNumber = null;
        previousDefaultAssistant.AnsweringNumberId = null;
        
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([previousDefaultAssistant, latestDefaultAssistant], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new SwitchAiSpeechDefaultAssistantResponse
        {
            Data = _mapper.Map<AiSpeechAssistantDto>(latestDefaultAssistant)
        };
    }

    public async Task<AddAiSpeechAssistantInboundRoutesResponse> AddAiSpeechAssistantInboundRoutesAsync(AddAiSpeechAssistantInboundRoutesCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.TargetNumber))
            throw new NullReferenceException("Target number is required!");
        
        await CheckNumberIfExistAsync(command.AgentId, command.Numbers.Select(x => x.PhoneNumber).ToList(), cancellationToken).ConfigureAwait(false);

        var routes = command.Numbers.Select(x => new AiSpeechAssistantInboundRoute
        {
            From = x.PhoneNumber,
            To = command.TargetNumber,
            IsFullDay = true,
            ForwardAssistantId = command.AssistantId,
            Remarks = x.Remarks,
            DayOfWeek = string.Empty
        }).ToList();
        
        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantInboundRouteAsync(routes, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AddAiSpeechAssistantInboundRoutesResponse
        {
            Data = _mapper.Map<List<AiSpeechAssistantInboundRouteDto>>(routes)
        };
    }

    public async Task<UpdateAiSpeechAssistantInboundRouteResponse> UpdateAiSpeechAssistantInboundRouteAsync(UpdateAiSpeechAssistantInboundRouteCommand command, CancellationToken cancellationToken)
    {
        var route = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantInboundRouteByIdAsync(command.RouteId, cancellationToken).ConfigureAwait(false);

        if (route == null) throw new Exception($"Could not find route with id {command.RouteId}");
        
        route.From = command.Number;
        route.Remarks = command.Remarks;
        
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantInboundRouteAsync([route], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdateAiSpeechAssistantInboundRouteResponse { Data = _mapper.Map<AiSpeechAssistantInboundRouteDto>(route) };
    }

    public async Task<DeleteAiSpeechAssistantInboundRoutesResponse> DeleteAiSpeechAssistantInboundRoutesAsync(DeleteAiSpeechAssistantInboundRoutesCommand command, CancellationToken cancellationToken)
    {
        var routes = await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantInboundRoutesAsync(command.RouteIds, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new DeleteAiSpeechAssistantInboundRoutesResponse
        {
            Data = _mapper.Map<List<AiSpeechAssistantInboundRouteDto>>(routes)
        };
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

    private async Task UpdateAssistantVoiceIfRequiredAsync(int assistantId, AiSpeechAssistantVoiceType voiceType, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantAsync(assistantId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get assistant for update model voice: {@Assistant}", assistant);

        if (assistant != null)
        {
            assistant.ModelVoice = ModelVoiceMapping(null, voiceType);
                
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
        var assistant = await InitialAssistantRelatedInfoAsync(command, cancellationToken).ConfigureAwait(false);
        
        await InitialAssistantKnowledgeAsync(command, assistant, cancellationToken).ConfigureAwait(false);

        return assistant;
    }

    private string ModelVoiceMapping(string voice, AiSpeechAssistantVoiceType? voiceType)
    {
        if (!string.IsNullOrWhiteSpace(voice)) return voice;
        
        if (!voiceType.HasValue) return "alloy";
        
        return voiceType.Value switch
        {
            AiSpeechAssistantVoiceType.Male => "ash",
            _ => "alloy"
        };
    }
    
    private async Task<Domain.AISpeechAssistant.AiSpeechAssistant> InitialAssistantRelatedInfoAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var (agent, number, isDefault) = command.AgentType switch
        {
            AgentType.AiKid => await InitialAiKidInternalAsync(command, cancellationToken).ConfigureAwait(false),
            AgentType.Agent => await InitialAiAgentInternalAsync(command, cancellationToken).ConfigureAwait(false),
            AgentType.Assistant => await InitialAssistantInternalAsync(command, cancellationToken).ConfigureAwait(false),
            AgentType.Restaurant => await InitialRestaurantInternalAsync(command, cancellationToken).ConfigureAwait(false),
            AgentType.PosCompanyStore => await InitialPosCompanyStoreInternalAsync(command, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException(nameof(command.AgentType))
        };
        
        var assistant = new Domain.AISpeechAssistant.AiSpeechAssistant
        {
            AgentId = agent.Id,
            ModelVoice = ModelVoiceMapping(agent.Voice, command.VoiceType),
            Name = command.AssistantName,
            AnsweringNumberId = number?.Id,
            AnsweringNumber = number?.Number,
            CreatedBy = _currentUser.Id.Value,
            ModelUrl = command.AgentType == AgentType.AiKid ? AiSpeechAssistantStore.AiKidDefaultUrl : AiSpeechAssistantStore.DefaultUrl,
            ModelProvider = AiSpeechAssistantProvider.OpenAi,
            Channel = command.Channels == null ? null : string.Join(",", command.Channels.Select(x => (int)x)),
            IsDisplay = command.IsDisplay,
            IsDefault = isDefault,
            ModelLanguage = command.AgentType == AgentType.Agent ? string.IsNullOrWhiteSpace(command.ModelLanguage) ? "English" : command.ModelLanguage : null,
            WaitInterval = agent.WaitInterval,
            IsTransferHuman = agent.IsTransferHuman
        };
        
        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var agentAssistant = new AgentAssistant
        {
            AgentId = agent.Id,
            AssistantId = assistant.Id
        };
        
        await _aiSpeechAssistantDataProvider.AddAgentAssistantsAsync([agentAssistant], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await UpdateAgentIfRequiredAsync(assistant, agent, cancellationToken).ConfigureAwait(false);

        await UpdateAiSpeechAssistantConfigsAsync(assistant, agent.TransferCallNumber, cancellationToken).ConfigureAwait(false);
        
        return assistant;
    }

    private async Task<Agent> AddAgentAsync(int? relateId, int? serviceProviderId, AgentType type, AgentSourceSystem sourceSystem, bool isDisplay, bool isSurface, CancellationToken cancellationToken)
    {
        var agent = new Agent
        {
            Type = type,
            RelateId = relateId,
            ServiceProviderId = serviceProviderId,
            IsDisplay = isDisplay,
            SourceSystem = sourceSystem,
            IsReceiveCall = type != AgentType.AiKid,
            IsSurface = isSurface
        };
        
        await _agentDataProvider.AddAgentAsync(agent, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return agent;
    }

    private async Task<NumberPool> DistributeNumberAsync(CancellationToken cancellationToken)
    {
        var number = await _aiSpeechAssistantDataProvider.GetNumberAsync(isUsed: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (number != null)
        {
            number.IsUsed = true;
            await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync([number], cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        
        return number;
    }
    
    private async Task<(Agent Agent, NumberPool Number, bool IsDefault)> InitialAiAgentInternalAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        if (!command.AgentId.HasValue) throw new ArgumentException("Agent id is required", nameof(command.AgentId));
        
        var agent = await _agentDataProvider.GetAgentByIdAsync(id: command.AgentId.Value, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (agent == null) throw new Exception($"Agent {command.AgentId} not found");
        
        var isDefault = await CheckAssistantIsDefaultAsync(agent.Id, cancellationToken).ConfigureAwait(false);
        
        var number = isDefault ? await DistributeNumberAsync(cancellationToken: cancellationToken).ConfigureAwait(false) : null;
        
        return (agent, number, isDefault);
    }

    private async Task<(Agent Agent, NumberPool Number, bool IsDefault)> InitialRestaurantInternalAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var restaurant = new Restaurant { Name = command.AssistantName };

        await _restaurantDataProvider.AddRestaurantAsync(restaurant, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var agent = await AddAgentAsync(restaurant.Id, command.ServiceProviderId, command.AgentType, command.SourceSystem, command.IsDisplay, false, cancellationToken);
        
        var number = await DistributeNumberAsync(cancellationToken).ConfigureAwait(false);
        
        return (agent, number, true);
    }

    private async Task<(Agent Agent, NumberPool Number, bool IsDefault)> InitialAiKidInternalAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var agent = await AddAgentAsync(null, command.ServiceProviderId, command.AgentType, command.SourceSystem, command.IsDisplay, false, cancellationToken).ConfigureAwait(false);
        
        if (command.Uuid.HasValue)
            await _aiSpeechAssistantDataProvider.AddAiKidAsync(new AiKid
            {
                AgentId = agent.Id,
                KidUuid = command.Uuid.Value
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return (agent, null, true);
    }

    private async Task<(Agent Agent, NumberPool Number, bool IsDefault)> InitialAssistantInternalAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var agent = await AddAgentAsync(null, command.ServiceProviderId, command.AgentType, command.SourceSystem, command.IsDisplay, false, cancellationToken).ConfigureAwait(false);
        
        var number = await DistributeNumberAsync(cancellationToken).ConfigureAwait(false);
        
        return (agent, number, true);
    }
    
    private async Task<(Agent Agent, NumberPool Number, bool IsDefault)> InitialPosCompanyStoreInternalAsync(AddAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        if (!command.StoreId.HasValue) throw new ArgumentException("Store id is required", nameof(command.AgentId));
        
        var agent = await AddAgentAsync(null, command.ServiceProviderId, AgentType.Assistant, command.SourceSystem, command.IsDisplay, false, cancellationToken).ConfigureAwait(false);

        var posAgent = new PosAgent
        {
            AgentId = agent.Id,
            StoreId = command.StoreId.Value
        };
        
        await _posDataProvider.AddPosAgentsAsync([posAgent], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var number = await DistributeNumberAsync(cancellationToken).ConfigureAwait(false);
        
        return (agent, number, true);
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

    private async Task InitialKnowledgeAsync(AiSpeechAssistantKnowledge latestKnowledge, List<KnowledgeCopyRelated> relateds, CancellationToken cancellationToken)
    {
        var latestKnowledgeJson = JObject.Parse(latestKnowledge.Json ?? "{}");

        var relatedJsons = relateds.Select(r => JObject.Parse(r.CopyKnowledgePoints ?? "{}"));
        
        var mergedJsonObj = new[] { latestKnowledgeJson }
            .Concat(relatedJsons)
            .Aggregate(new JObject(), (acc, j) =>
                { acc.Merge(j, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat }); return acc; });

        var mergedJson = mergedJsonObj.ToString(Formatting.None);
        
        latestKnowledge.IsActive = true;
        latestKnowledge.CreatedBy = _currentUser.Id.Value;
        latestKnowledge.Prompt = GenerateKnowledgePrompt(mergedJson);
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
    
    private async Task<List<Agent>> DeleteAssistantRelatedInfoAsync(List<int> assistantIds, bool isDeleteAgent, CancellationToken cancellationToken)
    {
        var result = await _aiSpeechAssistantDataProvider.GetAgentWithAssistantsAsync(assistantIds: assistantIds, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Count == 0) return [];

        var agentAssistants = result.Select(x => x.Item2).Where(x => x != null).ToList();
        
        await _aiSpeechAssistantDataProvider.DeleteAgentAssistantsAsync(agentAssistants, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var agents = result.Select(x => x.Item1).Distinct().ToList();

        if (isDeleteAgent)
            await _agentDataProvider.DeleteAgentsAsync(agents, cancellationToken: cancellationToken).ConfigureAwait(false);

        var knowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantActiveKnowledgesAsync(assistantIds, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await SyncCopiedKnowledgesIfRequiredAsync(knowledges, true, cancellationToken).ConfigureAwait(false);

        return agents;
    }

    private async Task UpdateAgentIfRequiredAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, Agent agent, CancellationToken cancellationToken)
    {
        if (agent.Type != AgentType.Assistant) return;
        
        agent.RelateId = assistant.Id;
        
        await _agentDataProvider.UpdateAgentsAsync([agent], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAiSpeechAssistantConfigsAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, string transferCallNumber, CancellationToken cancellationToken)
    {
        var configs = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantFunctionCallByAssistantIdsAsync([assistant.Id], assistant.ModelProvider, cancellationToken: cancellationToken).ConfigureAwait(false);
        var humanConcat = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantHumanContactByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get the human concat: {@HumanConcat}", humanConcat);
        
        var turnDetection = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.TurnDirection);
        var transferCallTool = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.Tool && x.Name == "transfer_call");
        
        Log.Information("Getting AI Speech Assistant Configs: {@TurnDetection} {@TransferCallTool}", turnDetection, transferCallTool);
        
        if (assistant.ModelProvider == AiSpeechAssistantProvider.OpenAi)
        {
            if (transferCallTool == null)
            {
                var content = new 
                {
                    type = "function",
                    name = "transfer_call",
                    description = "Triggered when the customer requests to transfer the call to a real person(e.g. 用户说 '转人工', '找真人', '接客服'), or when the customer is not satisfied with the current answer and wants someone else to serve him/her"
                };
            
                transferCallTool = new AiSpeechAssistantFunctionCall
                {
                    AssistantId = assistant.Id,
                    Name = "transfer_call",
                    Content = JsonConvert.SerializeObject(content),
                    Type = AiSpeechAssistantSessionConfigType.Tool,
                    ModelProvider = AiSpeechAssistantProvider.OpenAi,
                    IsActive = assistant.IsTransferHuman,
                };
            
                await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantFunctionCallsAsync([transferCallTool], cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                transferCallTool.IsActive = assistant.IsTransferHuman;
                
                await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantFunctionCallAsync([transferCallTool], cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            if (turnDetection == null)
            {
                var content = new 
                {
                    type = "server_vad",
                    threshold = 0.8f,
                    silence_duration_ms = assistant.WaitInterval == 0 ? 500 : assistant.WaitInterval
                };
                
                turnDetection = new AiSpeechAssistantFunctionCall
                {
                    AssistantId = assistant.Id,
                    Name = "turn_detection",
                    Content = JsonConvert.SerializeObject(content),
                    Type = AiSpeechAssistantSessionConfigType.TurnDirection,
                    ModelProvider = AiSpeechAssistantProvider.OpenAi,
                    IsActive = true
                };
                
                await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantFunctionCallsAsync([turnDetection], cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var content = JsonConvert.DeserializeObject<AiSpeechAssistantSessionTurnDetectionDto>(turnDetection.Content);

                if (!string.Equals(content.Type, "semantic_vad", StringComparison.OrdinalIgnoreCase))
                    content.SilenceDuratioMs = assistant.WaitInterval;
                
                turnDetection.Content = JsonConvert.SerializeObject(content);
                
                await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantFunctionCallAsync([turnDetection], cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            if (assistant.IsTransferHuman)
            {
                if (humanConcat == null)
                {
                    humanConcat = new AiSpeechAssistantHumanContact
                    {
                        AssistantId = assistant.Id,
                        HumanPhone = transferCallNumber
                    };
                    
                    await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantHumanContactAsync([humanConcat], cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    humanConcat.HumanPhone = transferCallNumber;
                    await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantHumanContactsAsync([humanConcat], cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task<bool> CheckAssistantIsDefaultAsync(int agentId, CancellationToken cancellationToken)
    {
        var agentAssistants = await _aiSpeechAssistantDataProvider.GetAgentAssistantsAsync(agentIds: [agentId], cancellationToken: cancellationToken).ConfigureAwait(false);

        if (agentAssistants == null) throw new Exception($"No agent assistant found with id {agentId}");
        
        return agentAssistants.Count == 0;
    }

    private async Task CheckNumberIfExistAsync(int agentId, List<string> whitelistNumbers, CancellationToken cancellationToken)
    {
        var routes = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantInboundRoutesByAgentIdAsync(agentId, cancellationToken).ConfigureAwait(false);

        Log.Information("Get the routes: {@Routes}", routes);
        
        foreach (var number in whitelistNumbers)
        {
            if(routes.Any(x => x.From.Trim() == number.Trim()))
                throw new Exception($"Number {number} already exist");
        }
    }

    public async Task<KonwledgeCopyAddedEvent> KonwledgeCopyAsync(KonwledgeCopyCommand command, CancellationToken cancellationToken)
    {
        if (command.TargetKnowledgeId == null || command.TargetKnowledgeId.Count == 0) throw new ArgumentException("TargetKnowledgeId is empty");

        if (command.TargetKnowledgeId.Contains(command.SourceKnowledgeId)) throw new Exception("Source knowledge cannot be included in targets");

        var copyFromKnowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(knowledgeId: command.SourceKnowledgeId, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (copyFromKnowledge == null) throw new InvalidOperationException("Source knowledge not found");

        Log.Information("KonwledgeCopy Source knowledge fetched. Id={SourceId}", copyFromKnowledge.Id);

        copyFromKnowledge.IsSyncUpdate = command.IsSyncUpdate;

        var copyToKnowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(command.TargetKnowledgeId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var copyToRelateds = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedByTargetKnowledgeIdAsync(copyToKnowledges.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);

        var relatedLookup = copyToRelateds?.GroupBy(x => x.TargetKnowledgeId)
                                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CreatedDate).ToList())
                            ?? new Dictionary<int, List<KnowledgeCopyRelated>>();

        Log.Information("KonwledgeCopy Related knowledge lookup built. KeysCount={Count}", relatedLookup.Count);

        var newCopeToKnowledges = new List<AiSpeechAssistantKnowledge>();

        foreach (var copyToKnowledge in copyToKnowledges)
        {
            var newCopyToKnowledge = await BuildNewCopyToKnowledgeAsync(copyToKnowledge, copyFromKnowledge, relatedLookup, cancellationToken).ConfigureAwait(false);
            newCopeToKnowledges.Add(newCopyToKnowledge);
        }

        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync(copyToKnowledges, true, cancellationToken).ConfigureAwait(false);
        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgesAsync(newCopeToKnowledges, true, cancellationToken).ConfigureAwait(false);

        Log.Information("KonwledgeCopy New copies inserted. newCopyToKnowledge={@newCopyToKnowledge}", newCopeToKnowledges);
        
        await BuildAndPersistCopyRelatedsAsync(copyFromKnowledge, copyToKnowledges, newCopeToKnowledges, relatedLookup, cancellationToken).ConfigureAwait(false);

        var knowledgeOldJsons = BuildKnowledgeOldJsons(copyToKnowledges, relatedLookup);

        Log.Information("KonwledgeCopy process completed successfully. SourceId={SourceId}", copyFromKnowledge.Id);

        return new KonwledgeCopyAddedEvent
        {
            CopyJson = copyFromKnowledge.Json,
            KnowledgeOldJsons = knowledgeOldJsons
        };
    }

    private async Task<AiSpeechAssistantKnowledge> BuildNewCopyToKnowledgeAsync(AiSpeechAssistantKnowledge copyToKnowledge, AiSpeechAssistantKnowledge copyFromKnowledge, 
        Dictionary<int, List<KnowledgeCopyRelated>> relatedLookup, CancellationToken cancellationToken)
    {
        Log.Information("KonwledgeCopy Processing target knowledge. TargetId={TargetId}", copyToKnowledge.Id);
        
        copyToKnowledge.IsActive = false;
        
        var copyToJson = JObject.Parse(copyToKnowledge.Json ?? "{}");
        var copyFromJson = JObject.Parse(copyFromKnowledge.Json ?? "{}");
        
        var copyToRelatedJsons = relatedLookup.TryGetValue(copyToKnowledge.Id, out var copyToRelated)
            ? copyToRelated.Select(r => JObject.Parse(r.CopyKnowledgePoints ?? "{}"))
            : Enumerable.Empty<JObject>();
        
        var mergedJsonObj = new[] { copyToJson }
            .Concat(copyToRelatedJsons)
            .Append(copyFromJson)
            .Aggregate(new JObject(), (acc, j) =>
            { acc.Merge(j, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat }); return acc; });

        var mergedJson = mergedJsonObj.ToString(Formatting.None);
        
        return new AiSpeechAssistantKnowledge
        {
            AssistantId = copyToKnowledge.AssistantId,
            Json = copyToKnowledge.Json,
            IsActive = true,
            CreatedBy = copyToKnowledge.CreatedBy,
            CreatedDate = DateTimeOffset.Now,
            Prompt = GenerateKnowledgePrompt(mergedJson),
            Version = await HandleKnowledgeVersionAsync(copyToKnowledge, cancellationToken)
        };
    }

    private async Task BuildAndPersistCopyRelatedsAsync(AiSpeechAssistantKnowledge copyFromKnowledge, List<AiSpeechAssistantKnowledge> oldCopyTos, 
        List<AiSpeechAssistantKnowledge> newCopyTos, Dictionary<int, List<KnowledgeCopyRelated>> relatedLookup, CancellationToken cancellationToken)
    {
        var result = new List<KnowledgeCopyRelated>();

        for (int i = 0; i < oldCopyTos.Count; i++)
        {
            var oldCopyTo = oldCopyTos[i];
            var newCopyTo = newCopyTos[i];
            
            if (relatedLookup.TryGetValue(oldCopyTo.Id, out var oldRelateds))
            {
                result.AddRange(oldRelateds.Select(r => new KnowledgeCopyRelated
                {
                    SourceKnowledgeId = r.SourceKnowledgeId,
                    TargetKnowledgeId = newCopyTo.Id,
                    CopyKnowledgePoints = r.CopyKnowledgePoints,
                    IsSyncUpdate = r.IsSyncUpdate
                }));
            }
            
            var copyFromJsonForRelated = BuildCopyFromJsonForRelated(copyFromKnowledge.Json);

            result.Add(new KnowledgeCopyRelated
            {
                SourceKnowledgeId = copyFromKnowledge.Id,
                TargetKnowledgeId = newCopyTo.Id,
                CopyKnowledgePoints = copyFromJsonForRelated,
                IsSyncUpdate = copyFromKnowledge.IsSyncUpdate
            });
        }
        
        await _aiSpeechAssistantDataProvider.AddKnowledgeCopyRelatedAsync(result, true, cancellationToken).ConfigureAwait(false);

        Log.Information("KonwledgeCopy KnowledgeCopyRelated inserted. Count={Count}", result.Count);
    }

    public static string BuildCopyFromJsonForRelated(string sourceJson)
    {
        if (string.IsNullOrWhiteSpace(sourceJson))
            return "{}";

        var sourceObj = JObject.Parse(sourceJson);
        var result = AppendCopySuffixToKeys(sourceObj);

        return result.ToString(Formatting.None);
    }

    private static JObject AppendCopySuffixToKeys(JObject source)
    {
        var result = new JObject();

        foreach (var prop in source.Properties())
        {
            var newKey =  prop.Name.EndsWith("-副本") ? prop.Name : prop.Name + "-副本";

            result[newKey] = CloneToken(prop.Value);
        }

        return result;
    }

    private static JToken CloneToken(JToken token)
    {
        return token.Type switch
        {
            JTokenType.Object => AppendCopySuffixToKeys((JObject)token),
            JTokenType.Array => new JArray(token.Select(t => CloneToken(t))),
            _ => token.DeepClone()
        };
    }

    private List<KnowledgeOldState> BuildKnowledgeOldJsons(List<AiSpeechAssistantKnowledge> copyToKnowledges, Dictionary<int, List<KnowledgeCopyRelated>> relatedLookup)
    {
        return copyToKnowledges.Select(copyToKnowledge =>
        {
            relatedLookup.TryGetValue(copyToKnowledge.Id, out var copyToRelated);
            var relatedJsons = copyToRelated?.Select(r => JObject.Parse(r.CopyKnowledgePoints ?? "{}")) ?? Enumerable.Empty<JObject>();

            var mergedOldJson = new[] { JObject.Parse(copyToKnowledge.Json ?? "{}") }
                .Concat(relatedJsons)
                .Aggregate(new JObject(), (acc, j) =>
                {
                    acc.Merge(j, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat });
                    return acc;
                });

            return new KnowledgeOldState
            {
                KnowledgeId = copyToKnowledge.Id,
                OldMergedJson = mergedOldJson.ToString(Formatting.None)
            };
        }).ToList();
    }
    
    public async Task<GetKonwledgesResponse> GetKonwledgesAsync(GetKonwledgesRequest request, CancellationToken cancellationToken)
    {
        var speechAssistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesByCompanyIdAsync(
            request.CompanyId, request.PageIndex, request.PageSize, request.AgentId, request.StoreId, request.KeyWord, cancellationToken).ConfigureAwait(false);

        return new GetKonwledgesResponse
        {
            Data = speechAssistants
        };
    }

    public async Task<GetKonwledgeRelatedResponse> GetKonwledgeRelatedAsync(GetKonwledgeRelatedRequest request, CancellationToken cancellationToken)
    {
        var (_, assistants) = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantsAsync(agentIds: new List<int> { request.AgentId }, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (assistants == null || assistants.Count == 0) 
            return new GetKonwledgeRelatedResponse { Data = new GetKonwledgeRelatedResponseData { DedicatedknowledgeDtos = new List<AiSpeechAssistantKnowledgeDto>() } };

        var assistantIds = assistants.Select(a => a.Id).ToList();

        var knowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantActiveKnowledgesAsync(assistantIds, cancellationToken).ConfigureAwait(false);

        if (knowledges == null || knowledges.Count == 0) return new GetKonwledgeRelatedResponse { Data = new GetKonwledgeRelatedResponseData { DedicatedknowledgeDtos = new List<AiSpeechAssistantKnowledgeDto>() } };

        var knowledgeIds = knowledges.Select(k => k.Id).ToList();

        var allCopyRelateds = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedByTargetKnowledgeIdAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);

        allCopyRelateds ??= new List<KnowledgeCopyRelated>();
        
        var sourceKnowledgeIds = allCopyRelateds.Select(r => r.SourceKnowledgeId).Distinct().ToList();
        var sourcerKnowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(sourceKnowledgeIds, cancellationToken).ConfigureAwait(false);

        var sourceKnowledgeMap = sourcerKnowledges.ToDictionary(t => t.Id);
        
        var enrichInfos = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedEnrichInfoAsync(
            sourcerKnowledges.Select(t => t.AssistantId).Distinct().ToList(), cancellationToken).ConfigureAwait(false);

        var enrichDict = enrichInfos.ToDictionary(x => x.AssistantId);

        var knowledgeDtoMap = knowledges.ToDictionary(k => k.Id, k => _mapper.Map<AiSpeechAssistantKnowledgeDto>(k));

        foreach (var related in allCopyRelateds)
        {
            if (!knowledgeDtoMap.TryGetValue(related.TargetKnowledgeId, out var dto))
                continue;

            dto.KnowledgeCopyRelateds ??= new List<KnowledgeCopyRelatedDto>();

            var relatedDto = _mapper.Map<KnowledgeCopyRelatedDto>(related);
            
            if (sourceKnowledgeMap.TryGetValue(related.SourceKnowledgeId, out var sourceKnowledge) && enrichDict.TryGetValue(sourceKnowledge.AssistantId, out var info))
            {
                relatedDto.RelatedFrom = $"{info.StoreName} - {info.AiAgentName} - {info.AssiatantName}";
            }

            dto.KnowledgeCopyRelateds.Add(relatedDto);
        }

        var dedicatedknowledges = knowledgeDtoMap.Values.ToList();

        return new GetKonwledgeRelatedResponse
        {
            Data = new GetKonwledgeRelatedResponseData
            {
                DedicatedknowledgeDtos = dedicatedknowledges
            }
        };
    }

    public async Task SyncCopiedKnowledgesIfRequiredAsync(List<AiSpeechAssistantKnowledge> sourceKnowledges, bool deleteKnowledge, CancellationToken cancellationToken)
    {
        if (sourceKnowledges == null || sourceKnowledges.Count == 0) return;

        var syncSources = sourceKnowledges.Where(x => x.IsSyncUpdate).ToList();
        
        if (syncSources.Count == 0) return;

        var sourceIds = syncSources.Select(x => x.Id).ToList();
        var sourceIdSet = new HashSet<int>(sourceIds);
        
        var copyRelateds = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedBySourceKnowledgeIdAsync(sourceIds, cancellationToken).ConfigureAwait(false);

        if (copyRelateds == null || copyRelateds.Count == 0) return;
        
        var targetKnowledgeIds = copyRelateds.Select(x => x.TargetKnowledgeId).Distinct().ToList();

        var oldTargets = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(targetKnowledgeIds, cancellationToken).ConfigureAwait(false);

        if (oldTargets == null || oldTargets.Count == 0) return;

        oldTargets.ForEach(x => x.IsActive = false);

        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync(oldTargets, true, cancellationToken).ConfigureAwait(false);

        var oldTargetMap = oldTargets.ToDictionary(x => x.Id);

        var allTargetRelations = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedByTargetKnowledgeIdAsync(targetKnowledgeIds, cancellationToken).ConfigureAwait(false) ?? new List<KnowledgeCopyRelated>();

        var sourceMap = syncSources.ToDictionary(x => x.Id);
        
        var relationsByTarget = allTargetRelations.GroupBy(r => r.TargetKnowledgeId).ToDictionary(
                g => g.Key, 
                g => g.OrderBy(r => r.CreatedDate).ToList());

        var newTargets = new List<AiSpeechAssistantKnowledge>();
        var newTargetEntries = new List<(int OldTargetId, AiSpeechAssistantKnowledge NewTarget)>();
        var newCopyRelateds = new List<KnowledgeCopyRelated>();
        
        foreach (var targetId in targetKnowledgeIds)
        {
            if (!oldTargetMap.TryGetValue(targetId, out var oldTarget) || !relationsByTarget.TryGetValue(targetId, out var relationsForTarget) || relationsForTarget.Count == 0)
                continue;

            var remainingRelations = deleteKnowledge
                ? relationsForTarget.Where(r => !sourceIdSet.Contains(r.SourceKnowledgeId)).ToList()
                : relationsForTarget;

            if (remainingRelations.Count == 0)
                continue;

            var mergedJsonObj = JObject.Parse(oldTarget.Json ?? "{}");

            foreach (var relation in remainingRelations)
            {
                JObject relationJson;

                if (!deleteKnowledge && sourceMap.TryGetValue(relation.SourceKnowledgeId, out var updatedSource)) 
                    relationJson = JObject.Parse(updatedSource.Json ?? "{}");
                else relationJson = JObject.Parse(relation.CopyKnowledgePoints ?? "{}");
                
                mergedJsonObj.Merge(relationJson, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat });
            }

            var mergedJson = mergedJsonObj.ToString(Formatting.None);

            var newTarget = new AiSpeechAssistantKnowledge
            {
                AssistantId = oldTarget.AssistantId,
                Json = oldTarget.Json,
                Brief = oldTarget.Brief,
                Greetings = oldTarget.Greetings,
                IsSyncUpdate = oldTarget.IsSyncUpdate,
                IsActive = true,
                CreatedBy = oldTarget.CreatedBy,
                CreatedDate = DateTimeOffset.Now,
                Prompt = GenerateKnowledgePrompt(mergedJson),
                Version = await HandleKnowledgeVersionAsync(oldTarget, cancellationToken).ConfigureAwait(false),
            };

            newTargets.Add(newTarget);
            newTargetEntries.Add((targetId, newTarget));

            foreach (var relation in remainingRelations)
            {
                var isUpdatedSource = !deleteKnowledge && sourceMap.ContainsKey(relation.SourceKnowledgeId);

                var copyPoints = isUpdatedSource ? sourceMap[relation.SourceKnowledgeId].Json : relation.CopyKnowledgePoints;
                
                newCopyRelateds.Add(new KnowledgeCopyRelated
                {
                    SourceKnowledgeId = relation.SourceKnowledgeId,
                    TargetKnowledgeId = targetId,
                    CopyKnowledgePoints = copyPoints,
                });
            }
        }

        if (newTargets.Count == 0) return;
        
        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgesAsync(newTargets, true, cancellationToken).ConfigureAwait(false);

        var newTargetMap = newTargetEntries.ToDictionary(x => x.OldTargetId, x => x.NewTarget.Id);

        foreach (var related in newCopyRelateds)
        {
            if (newTargetMap.TryGetValue(related.TargetKnowledgeId, out var newTargetId)) related.TargetKnowledgeId = newTargetId;
        }

        await _aiSpeechAssistantDataProvider.AddKnowledgeCopyRelatedAsync(newCopyRelateds, true, cancellationToken).ConfigureAwait(false);
    }
}