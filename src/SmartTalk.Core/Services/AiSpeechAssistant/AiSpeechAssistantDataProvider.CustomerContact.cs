using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Sales;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial class AiSpeechAssistantDataProvider
{
    public async Task<List<CrmCustomerContactPhoneMap>> GetCrmCustomerContactPhoneMapsByCompanyIdAsync(int companyId, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
            return [];

        return await _repository.Query<CrmCustomerContactPhoneMap>()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CrmCustomerContactPhoneMap> GetActiveCrmCustomerContactPhoneMapByAgentIdAndPhoneAsync(
        int agentId, string normalizedPhoneNumber, CancellationToken cancellationToken = default)
    {
        if (agentId <= 0 || string.IsNullOrWhiteSpace(normalizedPhoneNumber))
            return null;

        return await _repository.Query<CrmCustomerContactPhoneMap>()
            .Where(x => x.AgentId == agentId && x.IsActive && x.ContactPhoneNormalized == normalizedPhoneNumber)
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
