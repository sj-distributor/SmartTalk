using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AISpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider
{
    Task<List<AiSpeechAssistantDescription>> GetAiSpeechAssistantDescriptionsAsync(List<string> modelIds = null, CancellationToken cancellationToken = default);

    Task<bool> IsAiSpeechAssistantDescriptionExistedAsync(string itemDescription, CancellationToken cancellationToken = default);

    Task AddAiSpeechAssistantDescriptionsAsync(List<AiSpeechAssistantDescription> modelDescriptions, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateAiSpeechAssistantDescriptionsAsync(List<AiSpeechAssistantDescription> modelDescriptions, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteAiSpeechAssistantDescriptionsAsync(List<AiSpeechAssistantDescription> modelDescriptions, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider
{
    public async Task<List<AiSpeechAssistantDescription>> GetAiSpeechAssistantDescriptionsAsync(List<string> modelIds = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AiSpeechAssistantDescription>();

        if (modelIds is { Count: > 0 })
        {
            query = query.Where(x => modelIds.Contains(x.ModelId));
        }

        return await query.OrderBy(x => x.ModelId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsAiSpeechAssistantDescriptionExistedAsync(string itemDescription, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemDescription)) return false;

        return await _repository.Query<AiSpeechAssistantDescription>().AnyAsync(x => x.ModelValue ==itemDescription.Trim(), cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAiSpeechAssistantDescriptionsAsync(List<AiSpeechAssistantDescription> modelDescriptions, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (modelDescriptions == null || modelDescriptions.Count == 0) return;

        await _repository.InsertAllAsync(modelDescriptions, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAiSpeechAssistantDescriptionsAsync(List<AiSpeechAssistantDescription> modelDescriptions, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (modelDescriptions == null || modelDescriptions.Count == 0) return;

        await _repository.UpdateAllAsync(modelDescriptions, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAiSpeechAssistantDescriptionsAsync(List<AiSpeechAssistantDescription> modelDescriptions, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (modelDescriptions == null || modelDescriptions.Count == 0) return;

        await _repository.DeleteAllAsync(modelDescriptions, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
