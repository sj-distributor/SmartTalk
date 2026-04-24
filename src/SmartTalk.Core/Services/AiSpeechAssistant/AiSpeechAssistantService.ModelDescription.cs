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


        var modelIds = normalizedModels.Select(x => x.ModelId!).ToList(); 
        
        var existingModels = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantDescriptionsAsync(modelIds, cancellationToken).ConfigureAwait(false);

        var existingLookup = existingModels.ToDictionary(x => x.ModelId, x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var model in normalizedModels)
        {
            switch (model.Type)
            {
                case AiSpeechAssistantModelDescriptionSyncType.Delete:
                    if (!existingLookup.ContainsKey(model.ModelId!))
                        throw new Exception($"Could not find model for delete, modelId={model.ModelId}");
                    break;

                case AiSpeechAssistantModelDescriptionSyncType.Add:
                    if (existingLookup.ContainsKey(model.ModelId!))
                        throw new Exception($"Model already exists, modelId={model.ModelId}");
                    if (string.IsNullOrWhiteSpace(model.ModelValue))
                        throw new Exception($"Model value is required when adding, modelId={model.ModelId}");
                    break;

                case AiSpeechAssistantModelDescriptionSyncType.Update:
                    if (!existingLookup.ContainsKey(model.ModelId!))
                        throw new Exception($"Could not find model for update, modelId={model.ModelId}");
                    if (string.IsNullOrWhiteSpace(model.ModelValue))
                        throw new Exception($"Model value is required when updating, modelId={model.ModelId}");
                    break;

                default:
                    throw new NotSupportedException($"Unsupported sync type, type={model.Type}");
            }
        }

        var toDelete = new List<AiSpeechAssistantDescription>();
        var toAdd = new List<AiSpeechAssistantDescription>();
        var toUpdate = new List<AiSpeechAssistantDescription>();

        foreach (var model in normalizedModels)
        {
            switch (model.Type)
            {
                case AiSpeechAssistantModelDescriptionSyncType.Delete:
                    toDelete.Add(existingLookup[model.ModelId!]);
                    break;

                case AiSpeechAssistantModelDescriptionSyncType.Add:
                    toAdd.Add(new AiSpeechAssistantDescription
                    {
                        ModelId = model.ModelId!,
                        ModelValue = model.ModelValue!,
                        ModelDescription = model.ModelDescription
                    });
                    break;

                case AiSpeechAssistantModelDescriptionSyncType.Update:
                    var updateTarget = existingLookup[model.ModelId!];
                    updateTarget.ModelValue = model.ModelValue!;
                    updateTarget.ModelDescription = model.ModelDescription;
                    toUpdate.Add(updateTarget);
                    break;

                default:
                    throw new NotSupportedException($"Unsupported sync type, type={model.Type}");
            }
        }

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
