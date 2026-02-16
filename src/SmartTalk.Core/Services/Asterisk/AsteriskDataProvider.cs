using SmartTalk.Core.Data;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.Asterisk;
using Microsoft.EntityFrameworkCore;

namespace SmartTalk.Core.Services.Asterisk;

public interface IAsteriskDataProvider : IScopedDependency
{
    Task CreateAsteriskCdrAsync(AsteriskCdr cdr, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<AsteriskCdr> GetAsteriskCdrAsync(string src = null, DateTimeOffset? createdDate = null, CancellationToken cancellationToken = default);

    Task<List<RestaurantAsterisk>> GetRestaurantAsteriskAsync(
        string restaurantPhoneNumber = null, string twilioNumber = null, string hostRecords = null,
        string domainName = null, CancellationToken cancellationToken = default);

    Task UpdateRestaurantAsterisksAsync(List<RestaurantAsterisk> restaurantAsterisks, bool forceSave = true, CancellationToken cancellationToken = default);
}

public class AsteriskDataProvider : IAsteriskDataProvider
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository _repository;

    public AsteriskDataProvider(IUnitOfWork unitOfWork, IRepository repository)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
    }

    public async Task CreateAsteriskCdrAsync(AsteriskCdr cdr, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(cdr, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AsteriskCdr> GetAsteriskCdrAsync(string src = null, DateTimeOffset? createdDate = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AsteriskCdr>();

        if (!string.IsNullOrEmpty(src))
            query = query.Where(x => x.Src == src);

        if (createdDate.HasValue)
            query = query.Where(x => x.CreatedDate == createdDate.Value);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<RestaurantAsterisk>> GetRestaurantAsteriskAsync(
        string restaurantPhoneNumber = null, string twilioNumber = null, string hostRecords = null,
        string domainName = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<RestaurantAsterisk>();

        if (!string.IsNullOrEmpty(restaurantPhoneNumber))
            query = query.Where(x => x.RestaurantPhoneNumber == restaurantPhoneNumber);

        if (!string.IsNullOrEmpty(twilioNumber))
            query = query.Where(x => x.TwilioNumber == twilioNumber);

        if (!string.IsNullOrEmpty(hostRecords))
            query = query.Where(x => x.HostRecords == hostRecords);

        if (!string.IsNullOrEmpty(domainName))
            query = query.Where(x => x.DomainName == domainName);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateRestaurantAsterisksAsync(List<RestaurantAsterisk> restaurantAsterisks, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(restaurantAsterisks, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
