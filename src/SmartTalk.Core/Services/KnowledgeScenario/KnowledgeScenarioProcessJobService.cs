using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Services.KnowledgeScenario;

public interface IKnowledgeScenarioProcessJobService : IScopedDependency
{
    Task HandleSceneUpdatedAsync(int sceneId, CancellationToken cancellationToken);
}

public class KnowledgeScenarioProcessJobService : IKnowledgeScenarioProcessJobService
{
    private readonly ISmartiesClient _smartiesClient;
    private readonly IKnowledgeScenarioDataProvider _knowledgeScenarioDataProvider;
    private readonly IAiSpeechAssistantKnowledgePromptService _aiSpeechAssistantKnowledgePromptService;

    public KnowledgeScenarioProcessJobService(
        ISmartiesClient smartiesClient,
        IKnowledgeScenarioDataProvider knowledgeScenarioDataProvider,
        IAiSpeechAssistantKnowledgePromptService aiSpeechAssistantKnowledgePromptService)
    {
        _smartiesClient = smartiesClient;
        _knowledgeScenarioDataProvider = knowledgeScenarioDataProvider;
        _aiSpeechAssistantKnowledgePromptService = aiSpeechAssistantKnowledgePromptService;
    }

    public async Task HandleSceneUpdatedAsync(int sceneId, CancellationToken cancellationToken)
    {
        if (sceneId <= 0)
            return;

        var scene = (await _knowledgeScenarioDataProvider.GetKnowledgeScenesByIdsAsync([sceneId], cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (scene == null)
        {
            Log.Warning("HandleSceneUpdatedAsync skipped because scene was not found. SceneId={SceneId}", sceneId);
            return;
        }

        Log.Information("HandleSceneUpdatedAsync start. SceneId={SceneId}, Version={Version}", scene.Id, scene.Version);

        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsBySceneIdsAsync([scene.Id], cancellationToken).ConfigureAwait(false);

        var sceneItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdAsync(scene.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        await SnapshotKnowledgeSceneAsync(scene, sceneItems, scene.Version, true, cancellationToken).ConfigureAwait(false);

        Log.Information("HandleSceneUpdatedAsync completed. SceneId={SceneId}, SceneItemCount={SceneItemCount}", scene.Id, sceneItems.Count);
    }

    private async Task SnapshotKnowledgeSceneAsync(KnowledgeScene scene, List<KnowledgeSceneItem> sceneItems, string version, bool isActive, CancellationToken cancellationToken)
    {
        var (_, histories) = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoriesAsync(sceneId: scene.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        var previousHistory = histories.FirstOrDefault();
        var previousHistoryItems = previousHistory == null
            ? new List<KnowledgeSceneHistoryItem>()
            : await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoryItemsAsync([previousHistory.Id], cancellationToken).ConfigureAwait(false);

        histories.ForEach(h => h.IsActive = false);
        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneHistoriesAsync(histories, false, cancellationToken).ConfigureAwait(false);

        var brief = await GenerateSceneChangeBriefAsync(scene, sceneItems, previousHistory, previousHistoryItems, cancellationToken).ConfigureAwait(false);

        var historyEntity = new KnowledgeSceneHistory
        {
            SceneId = scene.Id,
            FolderId = scene.FolderId,
            Name = scene.Name,
            Description = scene.Description,
            Version = version,
            Brief = brief,
            Status = scene.Status,
            IsActive = isActive,
            CreatedAt = scene.CreatedAt,
            UpdatedAt = scene.UpdatedAt,
            SnapshotAt = DateTimeOffset.UtcNow
        };

        await _knowledgeScenarioDataProvider.AddKnowledgeSceneHistoryAsync(historyEntity, true, cancellationToken).ConfigureAwait(false);

        var historyItems = sceneItems.Select(item => new KnowledgeSceneHistoryItem
        {
            HistoryId = historyEntity.Id,
            SceneItemId = item.Id,
            Name = item.Name,
            Type = item.Type,
            Content = item.Content,
            FileName = item.FileName,
            CreatedAt = item.CreatedAt == default ? scene.CreatedAt : item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        }).ToList();

        await _knowledgeScenarioDataProvider.AddKnowledgeSceneHistoryItemsAsync(historyItems, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GenerateSceneChangeBriefAsync(KnowledgeScene scene, List<KnowledgeSceneItem> sceneItems, KnowledgeSceneHistory previousHistory, List<KnowledgeSceneHistoryItem> previousHistoryItems, CancellationToken cancellationToken)
    {
        try
        {
            var oldComparableJson = BuildSceneComparableJson(previousHistory, previousHistoryItems);
            var newComparableJson = BuildSceneComparableJson(scene, sceneItems);
            var diff = CompareJsons(oldComparableJson, newComparableJson);

            if (diff == null || !diff.HasValues)
                return "未命名改動";

            Log.Information("GenerateSceneChangeBriefAsync diff generated. SceneId={SceneId}, Diff={Diff}", scene.Id, diff);

            var brief = await GenerateKnowledgeChangeBriefAsync(diff.ToString(Formatting.None), cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(brief) ? "未命名改動" : brief;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Generate scene history brief error. SceneId={SceneId}", scene.Id);
            return "未命名改動";
        }
    }

    private static string BuildSceneComparableJson(KnowledgeSceneHistory history, IEnumerable<KnowledgeSceneHistoryItem> items)
    {
        if (history == null)
            return "{}";

        return BuildSceneComparableJsonCore(history.Name, history.Description, history.Status, items?.Select(x => new SceneComparableItem(x.Name, x.Type, x.Content, x.FileName)));
    }

    private static string BuildSceneComparableJson(KnowledgeScene scene, IEnumerable<KnowledgeSceneItem> items)
    {
        if (scene == null)
            return "{}";

        return BuildSceneComparableJsonCore(scene.Name, scene.Description, scene.Status, items?.Select(x => new SceneComparableItem(x.Name, x.Type, x.Content, x.FileName)));
    }

    private static string BuildSceneComparableJsonCore(string name, string description, KnowledgeSceneStatus status, IEnumerable<SceneComparableItem> items)
    {
        var obj = new JObject
        {
            ["name"] = name ?? string.Empty,
            ["description"] = description ?? string.Empty,
            ["status"] = (int)status
        };

        var itemArray = new JArray(
            (items ?? Enumerable.Empty<SceneComparableItem>())
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => (int)x.Type)
            .ThenBy(x => x.FileName, StringComparer.Ordinal)
            .ThenBy(x => x.Content, StringComparer.Ordinal)
            .Select(x => new JObject
            {
                ["name"] = x.Name ?? string.Empty,
                ["type"] = (int)x.Type,
                ["content"] = x.Content ?? string.Empty,
                ["fileName"] = x.FileName ?? string.Empty
            }));

        obj["sceneItems"] = itemArray;
        return obj.ToString(Formatting.None);
    }

    private static JObject CompareJsons(string oldJson, string newJson)
    {
        var result = new JObject();
        var oldObj = JObject.Parse(oldJson);
        var newObj = JObject.Parse(newJson);

        foreach (var property in oldObj.Properties())
        {
            var key = property.Name;
            var oldValue = property.Value;
            var newValue = newObj.TryGetValue(key, out var value) ? value : null;

            if (!JToken.DeepEquals(oldValue, newValue))
            {
                if (oldValue is JArray oldArray && newValue is JArray newArray)
                {
                    var arrayDiff = CompareJArrays(oldArray, newArray);
                    if (arrayDiff.Count > 0)
                        result[key] = arrayDiff;
                }
                else
                {
                    result[key] = new JArray
                    {
                        new JObject
                        {
                            ["old"] = oldValue,
                            ["new"] = newValue
                        }
                    };
                }
            }
        }

        foreach (var property in newObj.Properties())
        {
            var key = property.Name;
            if (!oldObj.ContainsKey(key))
            {
                result[key] = new JArray
                {
                    new JObject
                    {
                        ["old"] = null,
                        ["new"] = property.Value
                    }
                };
            }
        }

        return result;
    }

    private static JArray CompareJArrays(JArray oldArray, JArray newArray)
    {
        var diff = new JArray();
        var maxLength = Math.Max(oldArray.Count, newArray.Count);

        for (var i = 0; i < maxLength; i++)
        {
            var oldValue = i < oldArray.Count ? oldArray[i] : null;
            var newValue = i < newArray.Count ? newArray[i] : null;

            if (!JToken.DeepEquals(oldValue, newValue))
            {
                diff.Add(new JObject
                {
                    ["old"] = oldValue,
                    ["new"] = newValue
                });
            }
        }

        return diff;
    }

    private async Task<string> GenerateKnowledgeChangeBriefAsync(string query, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new()
                {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一個善於分析數據的助手，專門用於對內容變更進行簡要概括。請根據提供的變更內容，生成不超过 10 字的簡短總結，只需點明變更重點，無需過多解釋。")
                },
                new()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input: {query}, output:")
                }
            },
            Model = OpenAiModel.Gpt4oMini
        }, cancellationToken).ConfigureAwait(false);

        return completionResult?.Data?.Response;
    }

    private record SceneComparableItem(string Name, KnowledgeSceneItemType Type, string Content, string FileName);
}
