using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.Agent;

namespace SmartTalk.Core.Services.Agents;

public interface IAgentDataProvider : IScopedDependency
{
  Task<Domain.System.Agent> GetAgentAsync(AgentType type, int? relateId = null, string name = null, CancellationToken cancellationToken = default);
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
  
  public async Task<Agent> GetAgentAsync(AgentType type, int? relateId = null, string name = null, CancellationToken cancellationToken = default)
  {
    var baseQuery = _repository.Query<Agent>();

    var query = type switch
    {
      AgentType.Restaurant => from agent in baseQuery
        join restaurant in _repository.Query<Restaurant>() on agent.RelateId equals restaurant.Id
        where !string.IsNullOrEmpty(name) && restaurant.Name == name
        select agent,
      _ => throw new NotSupportedException(nameof(type))
    };

    return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
  }
}