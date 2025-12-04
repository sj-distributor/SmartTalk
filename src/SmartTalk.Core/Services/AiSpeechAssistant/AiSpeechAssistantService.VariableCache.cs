using SmartTalk.Core.Domain.Sales;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<GetAiSpeechAssistantKnowledgeVariableCacheResponse> GetAiSpeechAssistantKnowledgeVariableCacheAsync(
        GetAiSpeechAssistantKnowledgeVariableCacheRequest request, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantKnowledgeVariableCacheAsync(UpdateAiSpeechAssistantKnowledgeVariableCacheCommand command, CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantService
{
    public async Task<GetAiSpeechAssistantKnowledgeVariableCacheResponse> GetAiSpeechAssistantKnowledgeVariableCacheAsync(
        GetAiSpeechAssistantKnowledgeVariableCacheRequest request, CancellationToken cancellationToken = default)
    {
        var caches = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeVariableCachesAsync(request.CacheKey, request.Filter, cancellationToken);

        return new GetAiSpeechAssistantKnowledgeVariableCacheResponse
        {
            Data = new GetAiSpeechAssistantKnowledgeVariableCacheData
            {
                Caches = caches
            }
        };
    }

    public async Task UpdateAiSpeechAssistantKnowledgeVariableCacheAsync(
        UpdateAiSpeechAssistantKnowledgeVariableCacheCommand command, CancellationToken cancellationToken = default)
    {
        var caches = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeVariableCachesAsync(
            [command.CacheKey], command.Filter, cancellationToken);
        
        if (caches.Count == 0) return;

        var cache = caches.FirstOrDefault();
        cache.CacheValue = command.CacheValue;
        
        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgeVariableCachesAsync([cache], true, cancellationToken);
    }
}