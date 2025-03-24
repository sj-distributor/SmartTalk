using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.System;
using Microsoft.EntityFrameworkCore;
using AutoMapper.QueryableExtensions;
using SmartTalk.Core.Domain.Linphone;
using SmartTalk.Messages.Dto.Linphone;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Messages.Requests.Linphone;

namespace SmartTalk.Core.Services.Linphone;

public interface ILinphoneDataProvider : IScopedDependency
{
    Task AddLinphoneCdrAsync(List<LinphoneCdr> linphoneCdr, bool foreSave = true, CancellationToken cancellationToken = default);

    Task<List<LinphoneSip>> GetLinphoneSipAsync(CancellationToken cancellationToken = default);

    Task<(int, List<LinphoneHistoryDto>)> GetLinphoneHistoryAsync(List<int> agentIds = null, string caller = null, string restaurantName = null,
        int? pageSize = 10, int? pageIndex = 1, CancellationToken cancellationToken = default);

    Task<List<GetAgentBySipDto>> GetAgentBySipAsync(List<string> sips, CancellationToken cancellationToken);
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

    public async Task<(int, List<LinphoneHistoryDto>)> GetLinphoneHistoryAsync(List<int> agentIds = null, string caller = null, string restaurantName = null,
        int? pageSize = 10, int? pageIndex = 1, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<LinphoneCdr>();

        if (agentIds is { Count: > 0 })
            query = query.Where(x => agentIds.Contains(x.AgentId));

        if (!string.IsNullOrEmpty(caller))
            query = query.Where(x => x.Caller == caller);

        if (!string.IsNullOrEmpty(restaurantName))
            query = from cdr in query
                join agent in _repository.Query<Agent>() on cdr.AgentId equals agent.Id
                join restaurant in _repository.Query<Restaurant>() on agent.RelateId equals restaurant.Id
                where restaurant.Name.Contains(restaurantName)
                select cdr;

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        if (pageSize.HasValue && pageIndex.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        return (count, await query
            .OrderByDescending(x => x.CallDate)
            .ProjectTo<LinphoneHistoryDto>(_mapper.ConfigurationProvider).ToListAsync(cancellationToken).ConfigureAwait(false));
    }
    
    public async Task<List<GetAgentBySipDto>> GetAgentBySipAsync(List<string> sips, CancellationToken cancellationToken)
    {
        var query = _repository.Query<LinphoneSip>();

        if (sips is { Count: > 0 })
            query = query.Where(x => sips.Contains(x.Sip));
        
        var agents = (from sip in query
            join agent in _repository.Query<Agent>() on sip.AgentId equals agent.Id
            join restaurant in _repository.Query<Restaurant>() on agent.RelateId equals restaurant.Id
            select new GetAgentBySipDto
            {
                AgentId = agent.Id,
                Restaurant = restaurant.Name
            }).Distinct();
        
        return await agents.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}