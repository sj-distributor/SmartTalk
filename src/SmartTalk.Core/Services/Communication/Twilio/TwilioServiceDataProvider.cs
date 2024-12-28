using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Asterisk;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Requests.Twilio;

namespace SmartTalk.Core.Services.Communication.Twilio;

public interface ITwilioServiceDataProvider : IScopedDependency
{
    Task CreateAsteriskCdrAsync(AsteriskCdr cdr, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<AsteriskCdr> GetAsteriskCdrAsync(string src, CancellationToken cancellationToken);
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

    public async Task<AsteriskCdr> GetAsteriskCdrAsync(string src, CancellationToken cancellationToken)
    {
        var response = await _repository.FirstOrDefaultAsync<AsteriskCdr>(x => x.Src == src).ConfigureAwait(false);

        return response;
    }
}