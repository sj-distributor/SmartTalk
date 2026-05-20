using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<SyncAiSpeechAssistantDescriptionsResponse> SyncAiSpeechAssistantDescriptionsAsync(SyncAiSpeechAssistantDescriptionCommand command, CancellationToken cancellationToken);

    Task<CheckAiSpeechAssistantDescriptionExistsResponse> CheckAiSpeechAssistantDescriptionExistsAsync(CheckAiSpeechAssistantDescriptionExistsRequest request, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<CheckAiSpeechAssistantDescriptionExistsResponse> CheckAiSpeechAssistantDescriptionExistsAsync(CheckAiSpeechAssistantDescriptionExistsRequest request, CancellationToken cancellationToken)
    {
        var result = await _aiSpeechAssistantDataProvider.IsAiSpeechAssistantDescriptionExistedAsync(request.ItemDescription, cancellationToken).ConfigureAwait(false);

        return new CheckAiSpeechAssistantDescriptionExistsResponse
        {
            Data = new CheckAiSpeechAssistantDescriptionExistsResponseData
            {
                Result = result
            }
        };
    }

    public async Task<SyncAiSpeechAssistantDescriptionsResponse> SyncAiSpeechAssistantDescriptionsAsync(SyncAiSpeechAssistantDescriptionCommand command, CancellationToken cancellationToken)
    {
        var models = command.List?.ToList();
        
        if (models == null || models.Count == 0) return new SyncAiSpeechAssistantDescriptionsResponse();

        var normalizedModels = models.Select(x => new
        {
            x.Type,
            ModelId = x.Id?.Trim(),
            ModelValue = x.Value?.Trim(),
            ModelDescription = x.Description?.Trim()
        }).ToList();

        if (normalizedModels.Any(x => string.IsNullOrWhiteSpace(x.ModelId)))
            throw new Exception("Model id is required when syncing model descriptions.");

        var duplicatedModelIds = normalizedModels
            .GroupBy(x => x.ModelId, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicatedModelIds.Count > 0) throw new Exception($"Duplicate model ids in sync request, modelIds={string.Join(",", duplicatedModelIds)}");

        var grouped = normalizedModels.ToLookup(x => x.Type);
        var toDeleteModels = grouped[AiSpeechAssistantModelDescriptionSyncType.Delete].ToList();
        var toAddModels = grouped[AiSpeechAssistantModelDescriptionSyncType.Add].ToList();
        var toUpdateModels = grouped[AiSpeechAssistantModelDescriptionSyncType.Update].ToList();

        var modelIds = normalizedModels.Select(x => x.ModelId!).ToList();

        var existingModels = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantDescriptionsAsync(modelIds, cancellationToken).ConfigureAwait(false);

        var existingLookup = existingModels.ToDictionary(x => x.ModelId, x => x, StringComparer.OrdinalIgnoreCase);

        var existingAddIds = toAddModels
            .Where(x => existingLookup.ContainsKey(x.ModelId!))
            .Select(x => x.ModelId)
            .ToList();

        if (existingAddIds.Count > 0)
            throw new Exception($"Models already exist, modelIds={string.Join(",", existingAddIds)}");

        var effectiveDeleteModels = toDeleteModels.Where(x => existingLookup.ContainsKey(x.ModelId!)).ToList();
        var effectiveUpdateModels = toUpdateModels.Where(x => existingLookup.ContainsKey(x.ModelId!)).ToList();

        foreach (var model in toAddModels.Concat(effectiveUpdateModels))
        {
            if (string.IsNullOrWhiteSpace(model.ModelValue))
            {
                var operation = model.Type == AiSpeechAssistantModelDescriptionSyncType.Add ? "adding" : "updating";
                throw new Exception($"Model value is required when {operation}, modelId={model.ModelId}");
            }
        }

        var toDelete = effectiveDeleteModels.Select(x => existingLookup[x.ModelId!]).ToList();

        var toAdd = toAddModels.Select(model => new AiSpeechAssistantDescription
        {
            ModelId = model.ModelId!,
            ModelValue = model.ModelValue!,
            ModelDescription = model.ModelDescription
        }).ToList();

        var toUpdate = effectiveUpdateModels.Select(model =>
        {
            var updateTarget = existingLookup[model.ModelId!];
            updateTarget.ModelValue = model.ModelValue!;
            updateTarget.ModelDescription = model.ModelDescription;
            return updateTarget;
        }).ToList();

        if (toDelete.Count > 0)
            await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantDescriptionsAsync(toDelete, false, cancellationToken).ConfigureAwait(false);

        if (toUpdate.Count > 0)
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantDescriptionsAsync(toUpdate, false, cancellationToken).ConfigureAwait(false);

        if (toAdd.Count > 0)
            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantDescriptionsAsync(toAdd, false, cancellationToken).ConfigureAwait(false);

        return new SyncAiSpeechAssistantDescriptionsResponse
        {
            Data = new SyncAiSpeechAssistantDescriptionsResponseData { Result = true }
        };
    }
}
