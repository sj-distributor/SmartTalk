using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Dto.Agent;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SmartTalk.Core.Domain;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Services.Agents;

public interface IAgentDataProvider : IScopedDependency
{
    Task<Agent> GetAgentAsync(int? id = null, AgentType? type = null, int? relateId = null, string name = null, CancellationToken cancellationToken = default);
    
    Task<Agent> GetAgentByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<List<Agent>> GetAgentsAsync(List<int> agentIds = null, List<int> assistantIds = null, AgentType? type = null, CancellationToken cancellationToken = default);

    Task AddAgentAsync(Agent agent, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeleteAgentsAsync(List<Agent> agents, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAgentsAsync(List<Agent> agents, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<AgentPreviewDto>> GetAgentsByAgentTypeAsync<T>(AgentType agentType, List<int> agentIds = null, int? serviceProviderId = null, CancellationToken cancellationToken = default) where T : class, IEntity<int>, IAgent;

    Task<List<Agent>> GetAgentsWithAssistantsAsync(List<int> agentIds = null, string keyword = null, bool? isDefault = null, CancellationToken cancellationToken = default);
    
    Task<Agent> GetAgentByAssistantIdAsync(int assistantId, CancellationToken cancellationToken = default);

    Task<Agent> GetAgentByNumberAsync(string didNumber, int? assistantId = null, CancellationToken cancellationToken = default);
    
    Task<(int Count, List<Agent> Agents)> GetAgentsPagingAsync(int pageIndex, int pageSize, List<int> agentIds, string keyword = null, CancellationToken cancellationToken = default);
    
    Task<List<Agent>> GetAgentsByIdsAsync(List<int> ids, CancellationToken cancellationToken = default);

    Task<List<StoreAgentFlatDto>> GetStoreAgentsAsync(List<int> storeIds, CancellationToken cancellationToken = default);
}

public class AgentDataProvider : IAgentDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AgentDataProvider(IMapper mapper, IRepository repository, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }
  
    public async Task<Agent> GetAgentAsync(int? id = null, AgentType? type = null, int? relateId = null, string name = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Agent> query = null; 
        var baseQuery = _repository.Query<Agent>();

        if (type.HasValue)
        {
            query = type switch
            {
                AgentType.Restaurant => from agent in baseQuery
                    join restaurant in _repository.Query<Restaurant>() on agent.RelateId equals restaurant.Id
                    where !string.IsNullOrEmpty(name) && restaurant.Name == name
                    select agent,
                _ => throw new NotSupportedException(nameof(type))
            };
        }

        return query == null ? null : await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Agent> GetAgentByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<Agent>().Where(x => x.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Agent>> GetAgentsAsync(List<int> agentIds = null, List<int> assistantIds = null, AgentType? type = null, CancellationToken cancellationToken = default)
    {
        var query = from agent in _repository.Query<Agent>()
            join agentAssistant in _repository.Query<AgentAssistant>() on agent.Id equals agentAssistant.AgentId
            select new { agent, agentAssistant };

        if (agentIds is { Count: > 0 })
            query = query.Where(x => agentIds.Contains(x.agent.Id));
        
        if (assistantIds is { Count: > 0 })
            query = query.Where(x => assistantIds.Contains(x.agentAssistant.AssistantId));

        if (type.HasValue)
            query = query.Where(x => x.agent.Type == type.Value);
        
        return await query.Select(x => x.agent).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAgentAsync(Agent agent, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(agent, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAgentsAsync(List<Agent> agents, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(agents, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAgentsAsync(List<Agent> agents, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(agents, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AgentPreviewDto>> GetAgentsByAgentTypeAsync<T>(
        AgentType agentType, List<int> agentIds = null, int? serviceProviderId = null, CancellationToken cancellationToken = default) where T : class, IEntity<int>, IAgent
    {
        var query = from agent in _repository.Query<Agent>().Where(x => x.IsDisplay)
            join domain in _repository.Query<T>() on agent.RelateId equals domain.Id
            where agent.Type == agentType && (agentIds == null || agentIds.Contains(agent.Id)) 
                                          && (!serviceProviderId.HasValue || agent.ServiceProviderId == serviceProviderId.Value)
            select new AgentPreviewDto
            {
                Agent = agent,
                Domain = domain,
                CreatedDate = agent.CreatedDate
            };
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Agent>> GetAgentsWithAssistantsAsync(
        List<int> agentIds = null, string keyword = null, bool? isDefault = null, CancellationToken cancellationToken = default)
    {
        var query = from agent in _repository.Query<Agent>().Where(x => x.IsDisplay && x.IsSurface)
            join agentAssistant in _repository.Query<AgentAssistant>() on agent.Id equals agentAssistant.AgentId into agentAssistantGroups
            from agentAssistant in agentAssistantGroups.DefaultIfEmpty()
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id into assistantGroups
            from assistant in assistantGroups.DefaultIfEmpty()
            where agentIds != null && agentIds.Contains(agent.Id)
            select new { agent, assistant };
        
        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get agent and assistant pairs: {@Result}", result);
        
        return result.GroupBy(x => x.agent.Id).Select(x =>
        {
            var agentAssistantPair = x.First();
            agentAssistantPair.agent.Assistants = x.Select(a => a.assistant).Where(a => a != null).ToList();
            return agentAssistantPair.agent;
        }).ToList();
    }

    public async Task<Agent> GetAgentByAssistantIdAsync(int assistantId, CancellationToken cancellationToken = default)
    {
        var query = from agent in _repository.Query<Agent>().Where(x => x.IsDisplay)
            join agentAssistant in _repository.Query<AgentAssistant>() on agent.Id equals agentAssistant.AgentId
            where agentAssistant.AssistantId == assistantId
            select agent;
        
        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<Agent> GetAgentByNumberAsync(string didNumber, int? assistantId = null, CancellationToken cancellationToken = default)
    {
        var agentInfo =
            from agent in _repository.Query<Agent>()
            join agentAssistant in _repository.Query<AgentAssistant>() on agent.Id equals agentAssistant.AgentId
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id
            select new { agent, assistant };

        agentInfo = agentInfo.Where(x => assistantId.HasValue ? x.assistant.Id == assistantId.Value : x.assistant.AnsweringNumber == didNumber);

        return await agentInfo.Select(x => x.agent).FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int Count, List<Agent> Agents)> GetAgentsPagingAsync(int pageIndex, int pageSize, List<int> agentIds, string keyword = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<Agent>().Where(x => x.IsDisplay && x.IsSurface);
        
        if (agentIds != null)
            query = query.Where(x => agentIds.Contains(x.Id));

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Name.Contains(keyword));
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var agents = await query.OrderByDescending(x => x.CreatedDate).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        
        return (count, agents);
    }

    public async Task<List<Agent>> GetAgentsByIdsAsync(List<int> ids, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<Agent>().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<StoreAgentFlatDto>> GetStoreAgentsAsync(List<int> storeIds, CancellationToken cancellationToken = default)
    {
        var query =
            from posAgent in _repository.Query<PosAgent>()
            join agent in _repository.Query<Agent>() on posAgent.AgentId equals agent.Id
            join agentAssistant in _repository.Query<AgentAssistant>() on agent.Id equals agentAssistant.AgentId
            where (storeIds == null || storeIds.Count == 0 || storeIds.Contains(posAgent.StoreId)) && agent.IsSurface && agent.IsDisplay
            select new StoreAgentFlatDto
            {
                StoreId = posAgent.StoreId,
                AgentId = agent.Id,
                AgentName = agent.Name
            };

        return await query.Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}