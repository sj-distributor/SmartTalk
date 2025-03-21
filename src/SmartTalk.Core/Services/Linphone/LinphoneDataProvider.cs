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

    Task<List<GetLinphoneHistoryDto>> GetLinphoneHistoryAsync(List<int> agentIds, CancellationToken cancellationToken);

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

    public async Task<List<GetLinphoneHistoryDto>> GetLinphoneHistoryAsync(List<int> agentIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<LinphoneCdr>()
            .Where(x => agentIds.Contains(x.AgentId))
            .OrderByDescending(x => x.CallDate)
            .ProjectTo<GetLinphoneHistoryDto>(_mapper.ConfigurationProvider).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<GetAgentBySipDto>> GetAgentBySipAsync(List<string> sips, CancellationToken cancellationToken)
    {
        var query = from sip in  _repository.Query<LinphoneSip>().Where(x => sips.Contains(x.Sip))
            join agent in _repository.Query<Agent>() on sip.AgentId equals agent.Id
            join restaurant in _repository.Query<Restaurant>() on agent.RelateId equals restaurant.Id
            select new GetAgentBySipDto
            {
                AgentId = agent.Id,
                Restaurant = restaurant.Name
            };
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}