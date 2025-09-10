using System.Reflection;
using AutoMapper;
using SmartTalk.Core.Domain;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Messages.Commands.Agent;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Services.Agents;

public interface IAgentService : IScopedDependency
{
    Task<GetAgentsResponse> GetAgentsAsync(GetAgentsRequest request, CancellationToken cancellationToken);
    
    Task<GetSurfaceAgentsResponse> GetSurfaceAgentsAsync(GetSurfaceAgentsRequest request, CancellationToken cancellationToken);
    
    Task<AddAgentResponse> AddAgentAsync(AddAgentCommand command, CancellationToken cancellationToken);
    
    Task<UpdateAgentResponse> UpdateAgentAsync(UpdateAgentCommand command, CancellationToken cancellationToken);
    
    Task<DeleteAgentResponse> DeleteAgentAsync(DeleteAgentCommand command, CancellationToken cancellationToken);
}

public class AgentService : IAgentService
{
    private readonly IMapper _mapper;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public AgentService(IMapper mapper, IAgentDataProvider agentDataProvider, IRestaurantDataProvider restaurantDataProvider, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _mapper = mapper;
        _agentDataProvider = agentDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public async Task<GetAgentsResponse> GetAgentsAsync(GetAgentsRequest request, CancellationToken cancellationToken)
    {
        var agentTypes = request.AgentType.HasValue
            ? [request.AgentType.Value] : Enum.GetValues(typeof(AgentType)).Cast<AgentType>().ToList();
        
        var agentInfo = await GetAllAgentsAsync(agentTypes, request.AgentIds, request.ServiceProviderId, cancellationToken).ConfigureAwait(false);
        
        return new GetAgentsResponse { Data = agentInfo.OrderBy(x => x.CreatedDate).ToList() };
    }

    public async Task<GetSurfaceAgentsResponse> GetSurfaceAgentsAsync(GetSurfaceAgentsRequest request, CancellationToken cancellationToken)
    {
        var agents = await _agentDataProvider.GetAgentsWitAssistantsAsync(keyword: request.Keyword, isDefault: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GetSurfaceAgentsResponse
        {
            Data = _mapper.Map<List<AgentDto>>(agents)
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
            IsReceiveCall = command.IsReceivingCall
        };
        
        await _agentDataProvider.AddAgentAsync(agent, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        agent.RelateId = agent.Id;
        
        await _agentDataProvider.UpdateAgentsAsync([agent], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new AddAgentResponse { Data = _mapper.Map<AgentDto>(agent) };
    }

    public async Task<UpdateAgentResponse> UpdateAgentAsync(UpdateAgentCommand command, CancellationToken cancellationToken)
    {
        var agent = await _agentDataProvider.GetAgentByIdAsync(command.AgentId, cancellationToken).ConfigureAwait(false);
        
        _mapper.Map(command, agent);
        
        await _agentDataProvider.UpdateAgentsAsync([agent], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new UpdateAgentResponse { Data = _mapper.Map<AgentDto>(agent) };
    }

    public async Task<DeleteAgentResponse> DeleteAgentAsync(DeleteAgentCommand command, CancellationToken cancellationToken)
    {
        var agent = await _agentDataProvider.GetAgentByIdAsync(command.AgentId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (agent == null) throw new Exception($"Agent with id {command.AgentId} not found.");
        
        await _agentDataProvider.DeleteAgentsAsync([agent], cancellationToken: cancellationToken).ConfigureAwait(false);

        // if (agent.Assistants.Count == 0) return new DeleteAgentResponse { Data = _mapper.Map<AgentDto>(agent) };
        
        var (agentAssistants, assistants) = await _aiSpeechAssistantDataProvider.GetAgentAssistantWithAssistantsAsync(agent.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (agentAssistants != null && agentAssistants.Count != 0)
            await _aiSpeechAssistantDataProvider.DeleteAgentAssistantsAsync(agentAssistants, cancellationToken: cancellationToken);

        if (assistants == null || assistants.Count == 0) return new DeleteAgentResponse { Data = _mapper.Map<AgentDto>(agent) };
        
        await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantsAsync(assistants, cancellationToken: cancellationToken).ConfigureAwait(false);
            
        var defaultAssistant = assistants.Where(x => x.IsDefault).FirstOrDefault();

        if (defaultAssistant is not { AnsweringNumberId: not null }) return new DeleteAgentResponse { Data = _mapper.Map<AgentDto>(agent) };
        
        var number = await _aiSpeechAssistantDataProvider.GetNumberAsync(defaultAssistant.AnsweringNumberId.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
                
        number.IsUsed = false;
                
        await _aiSpeechAssistantDataProvider.UpdateNumberPoolAsync([number], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new DeleteAgentResponse { Data = _mapper.Map<AgentDto>(agent) };
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
}