using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AISpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider
{
    Task AddAiSpeechAssistantPremiseAsync(AiSpeechAssistantPremise premise, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<AiSpeechAssistantPremise> GetAiSpeechAssistantPremiseByAssistantIdAsync(int assistantId, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantPremiseAsync(AiSpeechAssistantPremise premise, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeleteAiSpeechAssistantPremiseAsync(AiSpeechAssistantPremise premise, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider
{
    public async Task AddAiSpeechAssistantPremiseAsync(AiSpeechAssistantPremise premise, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(premise, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantPremise> GetAiSpeechAssistantPremiseByAssistantIdAsync(int assistantId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AiSpeechAssistantPremise>().Where(x => x.AssistantId == assistantId).OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAiSpeechAssistantPremiseAsync(AiSpeechAssistantPremise premise, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(premise, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAiSpeechAssistantPremiseAsync(AiSpeechAssistantPremise premise, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(premise, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}