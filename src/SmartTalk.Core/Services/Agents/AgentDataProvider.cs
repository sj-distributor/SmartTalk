using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Dto.Agent;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain;
using SmartTalk.Core.Domain.Restaurants;

namespace SmartTalk.Core.Services.Agents;

public interface IAgentDataProvider : IScopedDependency
{
    Task<Agent> GetAgentAsync(int? id = null, AgentType? type = null, int? relateId = null, string name = null, CancellationToken cancellationToken = default);
    
    Task<Agent> GetAgentByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<List<Agent>> GetAgentsAsync(List<int> agentIds = null, AgentType? type = null, CancellationToken cancellationToken = default);

    Task AddAgentAsync(Agent agent, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeleteAgentsAsync(List<Agent> agents, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAgentsAsync(List<Agent> agents, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<AgentPreviewDto>> GetAgentsByAgentTypeAsync<T>(AgentType agentType, CancellationToken cancellationToken) where T : class, IEntity<int>, IAgent;
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

    public async Task<List<Agent>> GetAgentsAsync(List<int> agentIds = null, AgentType? type = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<Agent>();

        if (agentIds is { Count: > 0 })
            query = query.Where(x => agentIds.Contains(x.Id));

        if (type.HasValue)
            query = query.Where(x => x.Type == type.Value);
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<List<AgentPreviewDto>> GetAgentsByAgentTypeAsync<T>(AgentType agentType, CancellationToken cancellationToken) where T : class, IEntity<int>, IAgent
    {
        var query = from agent in _repository.Query<Agent>()
            join domain in _repository.Query<T>() on agent.RelateId equals domain.Id
            where agent.Type == agentType
            select new AgentPreviewDto
            {
                Agent = agent,
                Domain = domain,
                CreatedDate = agent.CreatedDate
            };
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}