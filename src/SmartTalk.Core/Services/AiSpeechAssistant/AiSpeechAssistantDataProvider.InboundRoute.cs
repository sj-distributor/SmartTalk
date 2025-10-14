using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AISpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider
{
    Task<List<AiSpeechAssistantInboundRoute>> GetAiSpeechAssistantInboundRoutesAsync(string targetNumber = null, bool? emergency = false, CancellationToken cancellationToken = default);
    
    Task AddAiSpeechAssistantInboundRoutesAsync(List<AiSpeechAssistantInboundRoute> inboundRoutes, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeleteAiSpeechAssistantInboundRoutesAsync(List<AiSpeechAssistantInboundRoute> inboundRoutes, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider
{
    public async Task<List<AiSpeechAssistantInboundRoute>> GetAiSpeechAssistantInboundRoutesAsync(string targetNumber = null, bool? emergency = false, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AiSpeechAssistantInboundRoute>();

        if (!string.IsNullOrEmpty(targetNumber))
            query = query.Where(x => x.To == targetNumber);

        if (emergency.HasValue)
            query = query.Where(x => x.Emergency == emergency.Value);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAiSpeechAssistantInboundRoutesAsync(List<AiSpeechAssistantInboundRoute> inboundRoutes, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(inboundRoutes, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAiSpeechAssistantInboundRoutesAsync(List<AiSpeechAssistantInboundRoute> inboundRoutes, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(inboundRoutes, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}