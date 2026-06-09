using Serilog;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
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
        if (string.IsNullOrWhiteSpace(request.ItemDescription))
            return new CheckAiSpeechAssistantDescriptionExistsResponse { Data = new CheckAiSpeechAssistantDescriptionExistsResponseData { Result = false } };

        var allDescriptions = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantDescriptionsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var descriptionList = allDescriptions.Aggregate(string.Empty, (current, description) => current + (string.IsNullOrWhiteSpace(description.ModelDescription)
            ? description.ModelValue + "\n"
            : $"{description.ModelValue}（{description.ModelDescription}）\n"));

        if (string.IsNullOrWhiteSpace(descriptionList))
            descriptionList = "（暂无维护的缩写，请仅凭物料名称判断）";

        var systemPrompt =
            "你是一名食品配送行业的物料描述理解助手。\n" +
            "用户会输入一段包含物料编号和物料描述的字符串，例如：80011149 Goc Vang Jasmine Rice  20024746CW BF Chuck Eye Roll N/O。\n" +
            "你的任务是：判断你能不能看懂这段物料描述指的是什么产品/物料。能看懂就返回 true，看不懂就返回 false。\n" +
            "判断时请注意：\n" +
            "1. 物料编号（纯数字或数字加字母组合，如 80011149、20024746CW）属于编号，不需要理解含义，直接忽略。\n" +
            "2. 描述部分请优先用食品配送行业的常识来理解（例如 Swai fillet 是巴沙鱼柳、Jasmine Rice 是茉莉香米、Chuck Eye Roll 是牛肩眼肉），只要能判断出它大致是什么产品就算看懂。\n" +
            "3. 即使描述（或其中的缩写、词语）没有出现在下方的系统缩写列表中，只要你能从字面意思理解它是什么产品，就算看懂，返回 true。缩写列表只是辅助，不是必须命中的条件。\n" +
            "4. 系统下方维护了一批本系统专用的物料缩写列表（每行格式：缩写 或 缩写（说明）），当描述里出现常识/字面无法直接理解的缩写时，再对照这个列表来辅助理解。缩写匹配区分大小写（例如 n/o 与 N/O 视为不相同）。\n" +
            "5. 只有当描述里出现既无法用常识或字面理解、也无法在缩写列表中找到的内容（如无意义的乱码、未知缩写）时，才返回 false。\n" +
            "请只返回 true 或 false，不得包含任何其他内容。\n\n" +
            "系统缩写列表：\n" + descriptionList;

        Log.Information("Sending item description check prompt to GPT: {Prompt}", systemPrompt);

        var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new ()
                {
                    Role = "system",
                    Content = new CompletionsStringContent(systemPrompt)
                },
                new ()
                {
                    Role = "user",
                    Content = new CompletionsStringContent("物料描述字符串：\n" + request.ItemDescription.Trim() + "\n\n")
                }
            },
            Model = OpenAiModel.Gpt4o
        }, cancellationToken).ConfigureAwait(false);

        Log.Information("GPT item description check result: {Result}", completionResult.Data.Response);

        return new CheckAiSpeechAssistantDescriptionExistsResponse
        {
            Data = new CheckAiSpeechAssistantDescriptionExistsResponseData
            {
                Result = bool.TryParse(completionResult.Data.Response, out var result) && result
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
