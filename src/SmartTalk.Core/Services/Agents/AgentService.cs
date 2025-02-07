using AutoMapper;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Services.Agents;

public interface IAgentService : IScopedDependency
{
    Task<GetAgentsResponse> GetAgentsAsync(GetAgentsRequest request, CancellationToken cancellationToken);
}

public class AgentService : IAgentService
{
    private readonly IMapper _mapper;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IRestaurantDataProvider _restaurantDataProvider;

    public AgentService(IMapper mapper, IAgentDataProvider agentDataProvider, IRestaurantDataProvider restaurantDataProvider)
    {
        _mapper = mapper;
        _agentDataProvider = agentDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
    }

    public async Task<GetAgentsResponse> GetAgentsAsync(GetAgentsRequest request, CancellationToken cancellationToken)
    {
        var agents = await _agentDataProvider.GetAgentsAsync(request.AgentType, cancellationToken).ConfigureAwait(false);
        
        var data = request.AgentType switch
        {
            AgentType.Restaurant => await GetAgentsWithRestaurantDataAsync(agents, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException(nameof(request.AgentType))
        };

        return new GetAgentsResponse { Data = data };
    }

    private async Task<List<GetAgentsResponseData>> GetAgentsWithRestaurantDataAsync(List<Agent> agents, CancellationToken cancellationToken)
    {
        var restaurants = await _restaurantDataProvider.GetRestaurantsAsync(agents.Select(x => x.RelateId).ToList(), cancellationToken).ConfigureAwait(false);

        return agents.Select(x => new GetAgentsResponseData
        {
            Agent = _mapper.Map<AgentDto>(x),
            Restaurant = _mapper.Map<RestaurantDto>(restaurants.Where(r => r.Id == x.RelateId).FirstOrDefault() ?? new Restaurant())
        }).ToList();
    }
}