using System.Reflection;
using AutoMapper;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Account;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Messages.Commands.Agent;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Services.Agents;

public interface IAgentService : IScopedDependency
{
    Task<GetAgentsResponse> GetAgentsAsync(GetAgentsRequest request, CancellationToken cancellationToken);
    
    Task<GetSurfaceAgentsResponse> GetSurfaceAgentsAsync(GetSurfaceAgentsRequest request, CancellationToken cancellationToken);
    
    Task<AddAgentResponse> AddAgentAsync(AddAgentCommand command, CancellationToken cancellationToken);
    
    Task<UpdateAgentResponse> UpdateAgentAsync(UpdateAgentCommand command, CancellationToken cancellationToken);
    
    Task<DeleteAgentResponse> DeleteAgentAsync(DeleteAgentCommand command, CancellationToken cancellationToken);
    
    Task<GetAgentByIdResponse> GetAgentByIdAsync(GetAgentByIdRequest request, CancellationToken cancellationToken);
    
    Task<GetAgentsWithAssistantsResponse> GetAgentsWithAssistantsAsync(GetAgentsWithAssistantsRequest request, CancellationToken cancellationToken);
}

public class AgentService : IAgentService
{
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IAccountDataProvider _accountDataProvider;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    
    public AgentService(IMapper mapper, ICurrentUser currentUser, IPosDataProvider posDataProvider, IAgentDataProvider agentDataProvider, IRestaurantDataProvider restaurantDataProvider, IAccountDataProvider accountDataProvider, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider, ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _posDataProvider = posDataProvider;
        _agentDataProvider = agentDataProvider;
        _accountDataProvider = accountDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
    }

    public async Task<GetAgentsResponse> GetAgentsAsync(GetAgentsRequest request, CancellationToken cancellationToken)
    {
        var agentTypes = request.AgentType.HasValue
            ? [request.AgentType.Value] : Enum.GetValues(typeof(AgentType)).Cast<AgentType>().ToList();

        var currentUser = await _accountDataProvider.GetUserAccountByUserIdAsync(_currentUser.Id.Value, cancellationToken: cancellationToken).ConfigureAwait(false);

        List<AgentPreviewDto> agentInfos;
        
        if (currentUser.AccountLevel == UserAccountLevel.ServiceProvider)
        {
            agentInfos = await GetAllAgentsAsync(agentTypes, null, request.ServiceProviderId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            agentInfos = await GetAllAgentsAsync(agentTypes, request.AgentIds??[], request.ServiceProviderId, cancellationToken).ConfigureAwait(false);
        }

        return new GetAgentsResponse { Data = agentInfos.OrderBy(x => x.CreatedDate).ToList() };
    }

    public async Task<GetSurfaceAgentsResponse> GetSurfaceAgentsAsync(GetSurfaceAgentsRequest request, CancellationToken cancellationToken)
    {
        var storeAgents = await _posDataProvider.GetPosAgentsAsync(storeIds: [request.StoreId], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get Surface Agent Ids: {@AgentIds}", storeAgents.Select(x => x.Id).ToList());
        
        var (count, agents) = await _agentDataProvider.GetAgentsPagingAsync(
            request.PageIndex, request.PageSize, storeAgents.Select(x => x.AgentId).ToList(), request.Keyword, cancellationToken).ConfigureAwait(false);

        var enrichAgents = _mapper.Map<List<AgentDto>>(agents);
        
        await EnrichAgentsAsync(enrichAgents, cancellationToken).ConfigureAwait(false);

        return new GetSurfaceAgentsResponse
        {
            Data = new GetSurfaceAgentsResponseData
            {
                Count = count,
                Agents = _mapper.Map<List<AgentDto>>(enrichAgents)
            }
        };
    }

    public async Task<AddAgentResponse> AddAgentAsync(AddAgentCommand command, CancellationToken cancellationToken)
    {
        var agent = new Agent
        {
            ServiceProviderId = command.ServiceProviderId,
            Type = AgentType.Agent,
            SourceSystem = AgentSourceSystem.Self,
            IsDisplay = true,
            Name = command.Name,
            Brief = command.Brief,
            Channel = command.Channel,
            IsReceiveCall = command.IsReceivingCall,
            IsSurface = true,
            Voice = command.Voice,
            WaitInterval = command.WaitInterval,
            IsTransferHuman = command.IsTransferHuman,
            TransferCallNumber = command.TransferCallNumber,
            ServiceHours = command.ServiceHours
        };
        
        await _agentDataProvider.AddAgentAsync(agent, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        agent.RelateId = agent.Id;
        
        await _agentDataProvider.UpdateAgentsAsync([agent], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var posAgent = new PosAgent
        {
            AgentId = agent.Id,
            StoreId = command.StoreId
        };
        
        await _posDataProvider.AddPosAgentsAsync([posAgent], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AddAgentResponse { Data = _mapper.Map<AgentDto>(agent) };
    }

    public async Task<UpdateAgentResponse> UpdateAgentAsync(UpdateAgentCommand command, CancellationToken cancellationToken)
    {
        var agent = await _agentDataProvider.GetAgentByIdAsync(command.AgentId, cancellationToken).ConfigureAwait(false);
        
        await ChangeNumberIfRequiredAsync(agent.Id, agent.Channel, command.Channel, cancellationToken).ConfigureAwait(false);
        
        _mapper.Map(command, agent);
        
        await _agentDataProvider.UpdateAgentsAsync([agent], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await HandleAiSpeechAssistantsAsync(agent, cancellationToken).ConfigureAwait(false);
        
        return new UpdateAgentResponse { Data = _mapper.Map<AgentDto>(agent) };
    }

    public async Task<DeleteAgentResponse> DeleteAgentAsync(DeleteAgentCommand command, CancellationToken cancellationToken)
    {
        var agent = await _agentDataProvider.GetAgentByIdAsync(command.AgentId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (agent == null) throw new Exception($"Agent with id {command.AgentId} not found.");
        
        await _agentDataProvider.DeleteAgentsAsync([agent], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await _posDataProvider.DeletePosAgentsByAgentIdsAsync([agent.Id], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var (agentAssistants, assistants) = await _aiSpeechAssistantDataProvider.GetAgentAssistantWithAssistantsAsync(agent.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (agentAssistants != null && agentAssistants.Count != 0)
            await _aiSpeechAssistantDataProvider.DeleteAgentAssistantsAsync(agentAssistants, cancellationToken: cancellationToken);

        if (assistants == null || assistants.Count == 0) return new DeleteAgentResponse { Data = _mapper.Map<AgentDto>(agent) };
        
        await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantsAsync(assistants, cancellationToken: cancellationToken).ConfigureAwait(false);
            
        var knowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantActiveKnowledgesAsync(
            assistants.Select(x=>x.Id).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var knowledge in knowledges)
        { _smartTalkBackgroundJobClient.Enqueue<IAiSpeechAssistantService>(x => x.SyncCopiedKnowledgesIfRequiredAsync(knowledge.Id, true,  false, CancellationToken.None)); }
        
        var defaultAssistant = assistants.Where(x => x.IsDefault).FirstOrDefault();

        if (defaultAssistant is not { AnsweringNumberId: not null }) return new DeleteAgentResponse { Data = _mapper.Map<AgentDto>(agent) };
        
        var number = await _aiSpeechAssistantDataProvider.GetNumberAsync(defaultAssistant.AnsweringNumberId.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
                
        number.IsUsed = false;
                
        await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync([number], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var routes = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantInboundRoutesByAgentIdAsync(agent.Id, cancellationToken).ConfigureAwait(false);
        
        await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantInboundRoutesAsync(routes, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new DeleteAgentResponse { Data = _mapper.Map<AgentDto>(agent) };
    }

    public async Task<GetAgentByIdResponse> GetAgentByIdAsync(GetAgentByIdRequest request, CancellationToken cancellationToken)
    {
        var agent = await _agentDataProvider.GetAgentByIdAsync(request.AgentId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (agent == null) throw new Exception($"Agent with id {request.AgentId} not found.");
        
        return new GetAgentByIdResponse { Data = _mapper.Map<AgentDto>(agent) };
    }

    public async Task<GetAgentsWithAssistantsResponse> GetAgentsWithAssistantsAsync(GetAgentsWithAssistantsRequest request, CancellationToken cancellationToken)
    {
        var storeAgents = await _posDataProvider.GetPosAgentsAsync(storeIds: request.StoreId.HasValue ? [request.StoreId.Value] : null, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Getting store agents: {@StoreAgents}.", storeAgents);
        
        var agents = await _agentDataProvider.GetAgentsWithAssistantsAsync(agentIds: storeAgents.Select(x => x.AgentId).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Getting store agents with assistants: {@Agents}.", agents);
        
        return new GetAgentsWithAssistantsResponse { Data = _mapper.Map<List<AgentDto>>(agents) };
    }

    private async Task<List<AgentPreviewDto>> GetAllAgentsAsync(List<AgentType> agentTypes, List<int> agentIds, int? serviceProviderId, CancellationToken cancellationToken)
    {
        var types = GetAllIAgentImplementations(agentTypes);
        var method = typeof(IAgentDataProvider).GetMethod(nameof(_agentDataProvider.GetAgentsByAgentTypeAsync));
    
        if (method == null)
            throw new InvalidOperationException("无法找到方法 GetAgentsByAgentTypeAsync");

        var result = new List<AgentPreviewDto>();

        foreach (var type in types)
        {
            var genericMethod = method.MakeGenericMethod(type.Type);
            var task = (Task)genericMethod.Invoke(_agentDataProvider, [type.AgentType, agentIds, serviceProviderId, cancellationToken]);

            if (task == null) continue;
            
            await task.ConfigureAwait(false);
            
            var agentList = (List<AgentPreviewDto>)((dynamic)task).Result;
            
            result.AddRange(agentList);
        }

        return result;
    }
    
    private static List<(AgentType AgentType, Type Type)> GetAllIAgentImplementations(List<AgentType> typesToInclude)
    {
        var allAgentTypes = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic)
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    return e.Types.Where(t => t != null)!;
                }
            }).Where(t => t.IsClass && !t.IsAbstract && typeof(IAgent).IsAssignableFrom(t)).ToList();

        return typesToInclude.Select(agentType => new
        {
            AgentType = agentType,
            MatchingType = allAgentTypes.FirstOrDefault(t => t.Name.Contains(agentType.ToString(), StringComparison.OrdinalIgnoreCase))
        }).Where(x => x.MatchingType != null).Select(x => (x.AgentType, x.MatchingType)).ToList();
    }

    private async Task EnrichAgentsAsync(List<AgentDto> agents, CancellationToken cancellationToken)
    {
        var agentAssistantPairs = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantsByAgentIdsAsync(agents.Select(x => x.Id).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var agent in agents)
        {
            var assistants = agentAssistantPairs.Where(x => x.Item1.AgentId == agent.Id).Select(x => x.Item2).ToList();
            
            agent.Assistants = _mapper.Map<List<AiSpeechAssistantDto>>(assistants);
        }
    }

    private async Task ChangeNumberIfRequiredAsync(int agentId, AiSpeechAssistantChannel? originalChannel, AiSpeechAssistantChannel newChannel, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByAgentIdAsync(agentId, cancellationToken).ConfigureAwait(false);
    
        if (assistant == null) return;
        
        if (originalChannel == newChannel) return;
        
        await HandleChannelChangeAsync(agentId, assistant, newChannel, cancellationToken);
    }
    
    private async Task HandleChannelChangeAsync(int agentId, Domain.AISpeechAssistant.AiSpeechAssistant assistant, AiSpeechAssistantChannel newChannel, CancellationToken cancellationToken)
    {
        switch (newChannel)
        {
            case AiSpeechAssistantChannel.Text:
                await ReleaseNumberAsync(agentId, assistant, cancellationToken);
                break;
            case AiSpeechAssistantChannel.PhoneChat:
                await AssignNumberAsync(agentId, assistant, cancellationToken);
                break;
        }
    }
    
    private async Task ReleaseNumberAsync(int agentId, Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken)
    {
        if (!assistant.AnsweringNumberId.HasValue) return;
    
        var number = await _aiSpeechAssistantDataProvider.GetNumberAsync(numberId: assistant.AnsweringNumberId, cancellationToken: cancellationToken).ConfigureAwait(false);
    
        if (number == null) return;
    
        number.IsUsed = false;
        await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync([number], cancellationToken: cancellationToken).ConfigureAwait(false);
    
        assistant.AnsweringNumberId = null;
        assistant.AnsweringNumber = string.Empty;
    
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await HandleAiSpeechInboundRoutesAsync(agentId, string.Empty, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task AssignNumberAsync(int agentId, Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken)
    {
        if (assistant.AnsweringNumberId.HasValue) return;
    
        var number = await _aiSpeechAssistantDataProvider.GetNumberAsync(isUsed: false, cancellationToken: cancellationToken).ConfigureAwait(false);
    
        if (number == null) return;
    
        number.IsUsed = true;
        await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync([number], cancellationToken: cancellationToken).ConfigureAwait(false);
    
        assistant.AnsweringNumberId = number.Id;
        assistant.AnsweringNumber = number.Number;
    
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await HandleAiSpeechInboundRoutesAsync(agentId, number.Number, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleAiSpeechInboundRoutesAsync(int agentId, string number, CancellationToken cancellationToken)
    {
        var routes = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantInboundRoutesByAgentIdAsync(agentId, cancellationToken).ConfigureAwait(false);
        
        routes.ForEach(x => x.To = number);
        
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantInboundRouteAsync(routes, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleAiSpeechAssistantsAsync(Agent agent, CancellationToken cancellationToken)
    {
        var assistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantsByAgentIdAsync(agent.Id, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Handle(update) default assistant info: {@Assistants}", assistants);
        
        if (assistants == null || assistants.Count == 0) return;
        
        assistants.ForEach(x =>
        {
            x.ModelVoice = agent.Voice;
            x.WaitInterval = agent.WaitInterval;
            x.IsTransferHuman = agent.IsTransferHuman;
        });
        
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync(assistants, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await HandleAiSpeechAssistantHumanContactAsync(assistants, agent.TransferCallNumber, cancellationToken).ConfigureAwait(false);
        
        await HandleAiSpeechAssistantConfigsAsync(agent, assistants, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task HandleAiSpeechAssistantHumanContactAsync(List<Domain.AISpeechAssistant.AiSpeechAssistant> assistants, string number, CancellationToken cancellationToken)
    {
        var humanContacts = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantHumanContactsAsync(
            assistants.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get assistants human contacts: {@HumanContacts}", humanContacts);
        
        if (string.IsNullOrWhiteSpace(number)) return;

        if (humanContacts?.Select(x => x.AssistantId).Distinct().Count() == assistants.Count)
        {
            humanContacts.ForEach(x => x.HumanPhone = number);
    
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantHumanContactsAsync(humanContacts, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Log.Information("Human contacts Compatibility Check");

            if (humanContacts != null && humanContacts.Count != 0)
                await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantHumanContactsAsync(humanContacts, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            humanContacts = assistants.Select(x => new AiSpeechAssistantHumanContact
            {
                AssistantId = x.Id,
                HumanPhone = number
            }).ToList();
        
            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantHumanContactAsync(humanContacts, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleAiSpeechAssistantConfigsAsync(Agent agent, List<Domain.AISpeechAssistant.AiSpeechAssistant> assistants, CancellationToken cancellationToken)
    {
        var specificAssistants = assistants.Where(x => x.ModelProvider == RealtimeAiProvider.OpenAi).ToList();
        
        var configs = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdsAsync(
            assistants.Select(x => x.Id).ToList(), RealtimeAiProvider.OpenAi, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var turnDetections = configs.Where(x => x.Type == AiSpeechAssistantSessionConfigType.TurnDirection).ToList();
        var transferCallTools = configs.Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool && x.Name == "transfer_call").ToList();
        
        Log.Information("Getting AI Speech Assistant Configs: {@TurnDetections} {@TransferCallTools}", turnDetections, transferCallTools);
        
        if (transferCallTools.Select(x => x.AssistantId).Distinct().Count() == specificAssistants.Count)
        {
            Log.Information("Normal isActive update");
            
            transferCallTools.ForEach(x => x.IsActive = agent.IsTransferHuman);
            
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantFunctionCallAsync(transferCallTools, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Log.Information("TransferCall Tools Compatibility Check");
            
            await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantFunctionCallsAsync(transferCallTools, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            var content = new 
            {
                type = "function",
                name = "transfer_call",
                description = "Triggered when the customer requests to transfer the call to a real person(e.g. 用户说 '转人工', '找真人', '接客服'), or when the customer is not satisfied with the current answer and wants someone else to serve him/her"
            };

            var newTools = specificAssistants.Select(x => new AiSpeechAssistantFunctionCall
            {
                AssistantId = x.Id,
                Name = "transfer_call",
                Content = JsonConvert.SerializeObject(content),
                Type = AiSpeechAssistantSessionConfigType.Tool,
                ModelProvider = RealtimeAiProvider.OpenAi,
                IsActive = agent.IsTransferHuman
            }).ToList();
            
            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantFunctionCallsAsync(newTools, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        
        if (turnDetections.Any(x => x.Content.Contains("semantic_vad"))) return;
        
        if (turnDetections.Select(x => x.AssistantId).Distinct().Count() == specificAssistants.Count)
        {
            Log.Information("Server vad update");
            
            var content = JsonConvert.DeserializeObject<AiSpeechAssistantSessionTurnDetectionDto>(turnDetections.First().Content);

            if (content.SilenceDuratioMs == agent.WaitInterval) return;
                
            content.SilenceDuratioMs = agent.WaitInterval;
            var contentJson = JsonConvert.SerializeObject(content);
                
            turnDetections.ForEach(x => x.Content = contentJson);
            
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantFunctionCallAsync(turnDetections, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Log.Information("Turn Detections Compatibility Check");
            
            await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantFunctionCallsAsync(turnDetections, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            var content = new 
            {
                type = "server_vad",
                threshold = 0.8f,
                silence_duration_ms = agent.WaitInterval == 0 ? 500 : agent.WaitInterval
            };
                
            var newDetections = specificAssistants.Select(x => new AiSpeechAssistantFunctionCall
            {
                AssistantId = x.Id,
                Name = "turn_detection",
                Content = JsonConvert.SerializeObject(content),
                Type = AiSpeechAssistantSessionConfigType.TurnDirection,
                ModelProvider = RealtimeAiProvider.OpenAi,
                IsActive = true
            }).ToList();
                
            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantFunctionCallsAsync(newDetections, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}