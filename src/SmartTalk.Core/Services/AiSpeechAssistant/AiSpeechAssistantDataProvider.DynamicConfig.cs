using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AISpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider
{
    Task<List<AiSpeechAssistantDynamicConfig>> GetAiSpeechAssistantDynamicConfigsAsync(bool? status = null, CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantDynamicConfig> GetAiSpeechAssistantDynamicConfigByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantDynamicConfig> UpdateAiSpeechAssistantDynamicConfigAsync(AiSpeechAssistantDynamicConfig config, bool forceSave = true,
        CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider
{
    public async Task<List<AiSpeechAssistantDynamicConfig>> GetAiSpeechAssistantDynamicConfigsAsync(bool? status = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AiSpeechAssistantDynamicConfig>();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await query.OrderBy(x => x.Level).ThenBy(x => x.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
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
}
