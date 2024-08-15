using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.Core.Services.System;

public interface IVerificationCodeDataProvider : IScopedDependency
{
    Task AddAsync(VerificationCode verificationCode, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAsync(VerificationCode verificationCode, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<VerificationCode> GetVerificationCodeAsync(
        int? userId, string identity, string recipient, string code = null, 
        UserAccountVerificationCodeMethod? verificationMethod = null, List<UserAccountVerificationCodeAuthenticationStatus> authenticationStatus = null, CancellationToken cancellationToken = default);
}

public class VerificationCodeDataProvider : IVerificationCodeDataProvider
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public VerificationCodeDataProvider(IRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task AddAsync(VerificationCode verificationCode, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(verificationCode, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(VerificationCode verificationCode, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(verificationCode, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<VerificationCode> GetVerificationCodeAsync(
        int? userId, string identity, string recipient, string code = null, 
        UserAccountVerificationCodeMethod? verificationMethod = null, List<UserAccountVerificationCodeAuthenticationStatus> authenticationStatus = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<VerificationCode>();
            
        if (userId.HasValue)
            query = query.Where(x => x.UserAccountId == userId);

        if (!string.IsNullOrEmpty(identity))
            query = query.Where(x => x.Identity == identity);

        if (!string.IsNullOrEmpty(recipient))
            query = query.Where(x => x.Recipient == recipient);

        if (!string.IsNullOrEmpty(code))
            query = query.Where(x => x.Code == code);

        if (verificationMethod.HasValue)
            query = query.Where(x => x.VerificationMethod == verificationMethod.Value);
        
        if (authenticationStatus != null && authenticationStatus.Any())
            query = query.Where(x => authenticationStatus.Contains(x.AuthenticationStatus));
        
        return await query
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}