using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Sales;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider
{
    Task<bool> HasCrmCustomerContactPhoneMapsAsync(CancellationToken cancellationToken = default);

    Task<List<CrmCustomerContactPhoneMap>> GetCrmCustomerContactPhoneMapsByCompanyIdAsync(int companyId, CancellationToken cancellationToken = default);

    Task<CrmCustomerContactPhoneMap> GetCrmCustomerContactPhoneMapByAgentIdAndPhoneAsync(int agentId, string normalizedPhoneNumber, CancellationToken cancellationToken = default);

    Task AddCrmCustomerContactPhoneMapsAsync(List<CrmCustomerContactPhoneMap> mappings, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateCrmCustomerContactPhoneMapsAsync(List<CrmCustomerContactPhoneMap> mappings, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider
{
    public async Task<bool> HasCrmCustomerContactPhoneMapsAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.Query<CrmCustomerContactPhoneMap>().AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<CrmCustomerContactPhoneMap>> GetCrmCustomerContactPhoneMapsByCompanyIdAsync(int companyId, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
            return [];

        return await _repository.Query<CrmCustomerContactPhoneMap>()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CrmCustomerContactPhoneMap> GetCrmCustomerContactPhoneMapByAgentIdAndPhoneAsync(
        int agentId, string normalizedPhoneNumber, CancellationToken cancellationToken = default)
    {
        if (agentId <= 0 || string.IsNullOrWhiteSpace(normalizedPhoneNumber))
            return null;

        return await _repository.Query<CrmCustomerContactPhoneMap>()
            .Where(x => x.AgentId == agentId && x.ContactPhoneNormalized == normalizedPhoneNumber)
            .OrderByDescending(x => x.LastModifiedDate ?? x.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddCrmCustomerContactPhoneMapsAsync(List<CrmCustomerContactPhoneMap> mappings, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (mappings == null || mappings.Count == 0)
            return;

        await _repository.InsertAllAsync(mappings, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateCrmCustomerContactPhoneMapsAsync(List<CrmCustomerContactPhoneMap> mappings, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (mappings == null || mappings.Count == 0)
            return;

        await _repository.UpdateAllAsync(mappings, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
