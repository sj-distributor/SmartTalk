using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AISpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider
{
    Task<List<AiSpeechAssistantDynamicConfig>> GetAiSpeechAssistantDynamicConfigsAsync(CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantDynamicConfig> GetAiSpeechAssistantDynamicConfigByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantDynamicConfig> UpdateAiSpeechAssistantDynamicConfigAsync(AiSpeechAssistantDynamicConfig config, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<AiSpeechAssistantDynamicConfigRelatingCompany>> GetAiSpeechAssistantDynamicConfigRelatingCompaniesAsync(List<int> configIds = null, List<int> companyIds = null, CancellationToken cancellationToken = default);
    
    Task DeleteAiSpeechAssistantDynamicConfigRelatingCompaniesAsync(List<AiSpeechAssistantDynamicConfigRelatingCompany> configRelatingCompanies, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task AddAiSpeechAssistantDynamicConfigRelatingCompaniesAsync(List<AiSpeechAssistantDynamicConfigRelatingCompany> configRelatingCompanies, bool forceSave = true,  CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider
{
    private IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProviderImplementation;

    public async Task<List<AiSpeechAssistantDynamicConfig>> GetAiSpeechAssistantDynamicConfigsAsync(CancellationToken cancellationToken = default)
    {
       return await _repository.Query<AiSpeechAssistantDynamicConfig>().OrderBy(x => x.Level).ThenBy(x => x.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantDynamicConfig> GetAiSpeechAssistantDynamicConfigByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AiSpeechAssistantDynamicConfig>()
            .Where(x => x.Id == id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantDynamicConfig> UpdateAiSpeechAssistantDynamicConfigAsync(AiSpeechAssistantDynamicConfig config, bool forceSave = true,
        CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(config, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return config;
    }

    public async Task<List<AiSpeechAssistantDynamicConfigRelatingCompany>> GetAiSpeechAssistantDynamicConfigRelatingCompaniesAsync(List<int> configIds = null, List<int> companyIds = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AiSpeechAssistantDynamicConfigRelatingCompany>().Where(x => configIds.Contains(x.ConfigId));

        if (companyIds is { Count: > 0 })
        {
            query = query.Where(x => companyIds.Contains(x.CompanyId));
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAiSpeechAssistantDynamicConfigRelatingCompaniesAsync(List<AiSpeechAssistantDynamicConfigRelatingCompany> configRelatingCompanies, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(configRelatingCompanies, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAiSpeechAssistantDynamicConfigRelatingCompaniesAsync(List<AiSpeechAssistantDynamicConfigRelatingCompany> configRelatingCompanies, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(configRelatingCompanies, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
