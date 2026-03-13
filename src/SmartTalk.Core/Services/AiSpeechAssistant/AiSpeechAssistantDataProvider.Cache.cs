using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider
{
    Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetAiSpeechAssistantKnowledgeVariableCachesAsync(List<string> cacheKeys = null, string filter = null, CancellationToken cancellationToken = default);
    
    Task<List<AiSpeechAssistantKnowledgeVariableCacheDto>> GetAiSpeechAssistantKnowledgeVariableCachesAsync(string cacheKey, string filter, CancellationToken cancellationToken = default);
    
    Task AddAiSpeechAssistantKnowledgeVariableCachesAsync(List<AiSpeechAssistantKnowledgeVariableCache> caches, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantKnowledgeVariableCachesAsync(List<AiSpeechAssistantKnowledgeVariableCache> caches, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider
{
    public async Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetAiSpeechAssistantKnowledgeVariableCachesAsync(
        List<string> cacheKeys = null, string filter = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AiSpeechAssistantKnowledgeVariableCache>();

        if (cacheKeys != null && cacheKeys.Count != 0)
            query = query.Where(x => cacheKeys.Contains(x.CacheKey));

        if (!string.IsNullOrEmpty(filter))
            query = query.Where(x => x.Filter == filter);
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantKnowledgeVariableCacheDto>> GetAiSpeechAssistantKnowledgeVariableCachesAsync(
        string cacheKey, string filter, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AiSpeechAssistantKnowledgeVariableCache>();

        if (!string.IsNullOrEmpty(cacheKey))
            query = query.Where(x => x.CacheKey == cacheKey);
        
        if (!string.IsNullOrEmpty(filter))
            query = query.Where(x => x.Filter == filter);
        
        return await query.ProjectTo<AiSpeechAssistantKnowledgeVariableCacheDto>(_mapper.ConfigurationProvider).ToListAsync(cancellationToken).ConfigureAwait(false);
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