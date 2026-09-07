using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Twilio;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.Twilio;

public interface ISmsOptOutService : IScopedDependency
{
    Task EnsureSenderNumberOwnedByCompanyAsync(int companyId, string senderNumber, CancellationToken cancellationToken = default);

    Task<bool> IsOptedOutAsync(int companyId, string customerNumber, CancellationToken cancellationToken = default);

    Task OptOutByInboundMessageAsync(string customerNumber, string senderNumber, CancellationToken cancellationToken = default);
}

public class SmsOptOutService : ISmsOptOutService
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SmsOptOutService(IRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task EnsureSenderNumberOwnedByCompanyAsync(int companyId, string senderNumber, CancellationToken cancellationToken = default)
    {
        var normalizedNumber = NormalizePhoneNumber(senderNumber);
        var sender = await _repository.Query<SmsSenderNumber>()
            .SingleOrDefaultAsync(x => x.PhoneNumber == normalizedNumber, cancellationToken)
            .ConfigureAwait(false);

        if (sender == null)
        {
            await _repository.InsertAsync(new SmsSenderNumber
            {
                CompanyId = companyId,
                PhoneNumber = normalizedNumber
            }, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (sender.CompanyId != companyId)
            throw new InvalidOperationException($"SMS sender number {normalizedNumber} is already assigned to another company.");
    }

    public async Task<bool> IsOptedOutAsync(int companyId, string customerNumber, CancellationToken cancellationToken = default)
    {
        var normalizedNumber = NormalizePhoneNumber(customerNumber);

        return await _repository.QueryNoTracking<SmsCustomerOptOut>()
            .AnyAsync(x => x.CompanyId == companyId && x.CustomerPhoneNumber == normalizedNumber, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task OptOutByInboundMessageAsync(string customerNumber, string senderNumber, CancellationToken cancellationToken = default)
    {
        var normalizedSenderNumber = NormalizePhoneNumber(senderNumber);
        var normalizedCustomerNumber = NormalizePhoneNumber(customerNumber);
        var sender = await _repository.QueryNoTracking<SmsSenderNumber>()
            .SingleOrDefaultAsync(x => x.PhoneNumber == normalizedSenderNumber, cancellationToken)
            .ConfigureAwait(false);

        if (sender == null)
            return;

        var existing = await _repository.Query<SmsCustomerOptOut>()
            .SingleOrDefaultAsync(x => x.CompanyId == sender.CompanyId && x.CustomerPhoneNumber == normalizedCustomerNumber, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
            return;

        try
        {
            await _repository.InsertAsync(new SmsCustomerOptOut
            {
                CompanyId = sender.CompanyId,
                CustomerPhoneNumber = normalizedCustomerNumber
            }, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
        {
            // Twilio may retry the same callback concurrently; the unique key makes this idempotent.
        }
    }

    public static string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));

        return phoneNumber.Trim()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty);
    }
}
