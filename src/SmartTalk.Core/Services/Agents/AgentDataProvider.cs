using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Dto.Agent;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Restaurants;

namespace SmartTalk.Core.Services.Agents;

public interface IAgentDataProvider : IScopedDependency
{
    Task<Agent> GetAgentAsync(int? id = null, AgentType? type = null, int? relateId = null, string name = null, CancellationToken cancellationToken = default);
    
    Task<Agent> GetAgentByIdAsync(int id, CancellationToken cancellationToken);

    Task<List<Agent>> GetAgentsAsync(AgentType type, CancellationToken cancellationToken);
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

    public async Task<Agent> GetAgentByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.Query<Agent>().Where(x => x.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Agent>> GetAgentsAsync(AgentType type, CancellationToken cancellationToken)
    {
        return await _repository.Query<Agent>().Where(x => x.Type == type).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}