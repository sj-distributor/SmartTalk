using System.Reflection;
using AutoMapper;
using SmartTalk.Core.Domain;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Account;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Core.Services.Agents;

public interface IAgentService : IScopedDependency
{
    Task<GetAgentsResponse> GetAgentsAsync(GetAgentsRequest request, CancellationToken cancellationToken);
}

public class AgentService : IAgentService
{
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IAccountDataProvider _accountDataProvider;
    private readonly IRestaurantDataProvider _restaurantDataProvider;

    public AgentService(IMapper mapper, ICurrentUser currentUser, IAgentDataProvider agentDataProvider, IRestaurantDataProvider restaurantDataProvider, IAccountDataProvider accountDataProvider)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _agentDataProvider = agentDataProvider;
        _accountDataProvider = accountDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
    }

    public async Task<GetAgentsResponse> GetAgentsAsync(GetAgentsRequest request, CancellationToken cancellationToken)
    {
        var agentTypes = request.AgentType.HasValue
            ? [request.AgentType.Value] : Enum.GetValues(typeof(AgentType)).Cast<AgentType>().ToList();

        var currentUser = await _accountDataProvider.GetUserAccountByUserIdAsync(_currentUser.Id.Value, cancellationToken).ConfigureAwait(false);

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