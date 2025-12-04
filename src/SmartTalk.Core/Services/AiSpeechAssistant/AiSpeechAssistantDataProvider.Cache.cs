using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Sales;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider
{
    Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetAiSpeechAssistantKnowledgeVariableCachesAsync(List<string> cacheKeys, CancellationToken cancellationToken = default);
    
    Task AddAiSpeechAssistantKnowledgeVariableCachesAsync(List<AiSpeechAssistantKnowledgeVariableCache> caches, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantKnowledgeVariableCachesAsync(List<AiSpeechAssistantKnowledgeVariableCache> caches, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider
{
    public async Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetAiSpeechAssistantKnowledgeVariableCachesAsync(
        List<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AiSpeechAssistantKnowledgeVariableCache>();

        if (cacheKeys.Count != 0)
            query = query.Where(x => cacheKeys.Contains(x.CacheKey));
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAiSpeechAssistantKnowledgeVariableCachesAsync(
        List<AiSpeechAssistantKnowledgeVariableCache> caches, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(caches, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAiSpeechAssistantKnowledgeVariableCachesAsync(
        List<AiSpeechAssistantKnowledgeVariableCache> caches, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(caches, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}