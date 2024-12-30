using SmartTalk.Core.Data;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.Asterisk;
using Microsoft.EntityFrameworkCore;

namespace SmartTalk.Core.Services.Communication.Twilio;

public interface ITwilioServiceDataProvider : IScopedDependency
{
    Task CreateAsteriskCdrAsync(AsteriskCdr cdr, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<AsteriskCdr> GetAsteriskCdrAsync(string src = null, DateTimeOffset? createdDate = null, CancellationToken cancellationToken = default);
}

public class TwilioServiceDataProvider : ITwilioServiceDataProvider
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository _repository;
    
    public TwilioServiceDataProvider(IUnitOfWork unitOfWork, IRepository repository)
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
}