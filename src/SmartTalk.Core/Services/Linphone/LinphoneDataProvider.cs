using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.System;
using Microsoft.EntityFrameworkCore;
using AutoMapper.QueryableExtensions;
using SmartTalk.Core.Domain.Asterisk;
using SmartTalk.Core.Domain.Linphone;
using SmartTalk.Messages.Dto.Linphone;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Messages.Enums.Linphone;
using SmartTalk.Messages.Requests.Linphone;

namespace SmartTalk.Core.Services.Linphone;

public interface ILinphoneDataProvider : IScopedDependency
{
    Task AddLinphoneCdrAsync(List<LinphoneCdr> linphoneCdr, bool foreSave = true, CancellationToken cancellationToken = default);

    Task<List<LinphoneSip>> GetLinphoneSipAsync(CancellationToken cancellationToken = default);

    Task<(int, List<LinphoneHistoryDto>)> GetLinphoneHistoryAsync(List<int> agentIds = null, string caller = null, string restaurantName = null, List<LinphoneStatus> status = null,
        int? pageSize = 10, int? pageIndex = 1, CancellationToken cancellationToken = default);

    Task<List<GetAgentBySipDto>> GetAgentBySipAsync(List<string> sips, CancellationToken cancellationToken);

    Task<LinphoneCdr> GetLinphoneCdrAsync(CancellationToken cancellationToken);

    Task UpdateLinphoneCdrAsync(LinphoneCdr linphoneCdr, bool foreSave = true, CancellationToken cancellationToken = default);

    Task AddCdrAsync(List<Cdr> cdrs, bool foreSave = true, CancellationToken cancellationToken = default);

    Task<List<Restaurant>> GetRestaurantPhoneNumberAsync(string toRestaurantName = null, CancellationToken cancellationToken = default);

    Task<List<Cdr>> GetCdrsAsync(long startTime, long endTime, CancellationToken cancellationToken = default);

    Task<Dictionary<string, string>> GetRestaurantSipAsync(CancellationToken cancellationToken);
}

public class LinphoneDataProvider : ILinphoneDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    
    public LinphoneDataProvider(IMapper mapper,IRepository repository, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task AddLinphoneCdrAsync(List<LinphoneCdr> linphoneCdr, bool foreSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(linphoneCdr, cancellationToken).ConfigureAwait(false);

        if (foreSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<LinphoneSip>> GetLinphoneSipAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.Query<LinphoneSip>().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int, List<LinphoneHistoryDto>)> GetLinphoneHistoryAsync(List<int> agentIds = null, string caller = null,
        string restaurantName = null, List<LinphoneStatus> status = null, int? pageSize = 10, int? pageIndex = 1, CancellationToken cancellationToken = default)
    {
        var originalQuery = _repository.Query<LinphoneCdr>();

        if (agentIds is { Count: > 0 })
            originalQuery = originalQuery.Where(x => agentIds.Contains(x.AgentId));

        if (!string.IsNullOrEmpty(caller))
            originalQuery = originalQuery.Where(x => x.Caller == caller);

        if (status is { Count: > 0 })
            originalQuery = originalQuery.Where(x => status.Contains(x.Status));

        var targetQuery = from cdr in originalQuery
            join agent in _repository.Query<Agent>() on cdr.AgentId equals agent.Id
            join restaurant in _repository.Query<Restaurant>() on agent.RelateId equals restaurant.Id
            select new LinphoneHistoryDto
            {
                Id = cdr.Id,
                CallDate = cdr.CallDate,
                Caller = cdr.Caller,
                AnotherName = restaurant.AnotherName,
                Targetter = cdr.Targetter,
                Status = cdr.Status
            };

        if (!string.IsNullOrEmpty(restaurantName))
            targetQuery = targetQuery.Where(x => x.AnotherName.Contains(restaurantName));

        var count = await targetQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        
        if (pageSize.HasValue && pageIndex.HasValue)
            targetQuery = targetQuery.OrderByDescending(x => x.CallDate).Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        return (count, await targetQuery
            .ToListAsync(cancellationToken).ConfigureAwait(false));
    }
    
    public async Task<List<GetAgentBySipDto>> GetAgentBySipAsync(List<string> sips, CancellationToken cancellationToken)
    {
        var query = _repository.Query<LinphoneSip>();

        if (sips is { Count: > 0 })
            query = query.Where(x => sips.Contains(x.Sip));
        
        var sipData = await query
            .Select(sip => new 
            { 
                sip.AgentId,
                sip.RelatedAgentIds 
            }).ToListAsync(cancellationToken);
        
        var agentIdSet = new HashSet<int>();
        
        foreach (var sip in sipData)
        {
            agentIdSet.Add(sip.AgentId);
            foreach (var relatedId in sip.RelatedAgentIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(relatedId.Trim(), out var id))
                    agentIdSet.Add(id);
            }
        }

        var validAgentIds = agentIdSet.ToList();
        
        var agents = await _repository.Query<Agent>()
            .Where(agent => validAgentIds.Contains(agent.Id))
            .Join(_repository.Query<Restaurant>(),
                agent => agent.RelateId,
                restaurant => restaurant.Id,
                (agent, restaurant) => new GetAgentBySipDto
                {
                    AgentId = agent.Id,
                    Restaurant = restaurant.Name
                })
            .Distinct()
            .ToListAsync(cancellationToken);
        
        return agents;
    }

    public async Task<LinphoneCdr> GetLinphoneCdrAsync(CancellationToken cancellationToken)
    {
        return await _repository.Query<LinphoneCdr>()
            .OrderByDescending(x => x.CallDate)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateLinphoneCdrAsync(LinphoneCdr linphoneCdr, bool foreSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(linphoneCdr, cancellationToken).ConfigureAwait(false);

        if (foreSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddCdrAsync(List<Cdr> cdrs, bool foreSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(cdrs, cancellationToken).ConfigureAwait(false);

        if (foreSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Restaurant>> GetRestaurantPhoneNumberAsync(string toRestaurantName = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<Restaurant>();

        if (!string.IsNullOrEmpty(toRestaurantName))
            query = query.Where(x => x.AnotherName == toRestaurantName);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Cdr>> GetCdrsAsync(long startTime, long endTime, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<Cdr>().Where(x => x.Uniqueid > startTime && x.Uniqueid < endTime).ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetRestaurantSipAsync(CancellationToken cancellationToken)
    {
        var query = from sips in _repository.Query<LinphoneSip>()
            join agents in _repository.Query<Agent>() on sips.AgentId equals agents.Id
            join restaurants in _repository.Query<Restaurant>() on agents.RelateId equals restaurants.Id
            select new
            {
                sips.Sip,
                restaurants.Name
            };
        
        return await query.ToDictionaryAsync(x => x.Sip, x => x.Name, cancellationToken).ConfigureAwait(false);
    }
}