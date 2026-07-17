using System.Text;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.KnowledgeScenario;
using Serilog;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public interface IAiSpeechAssistantKnowledgePromptService : IScopedDependency
{
    string BuildFinalPrompt(AiSpeechAssistantKnowledge knowledge);

    Task<string> GenerateScenePromptAsync(int knowledgeId, CancellationToken cancellationToken);

    Task RefreshScenePromptsAsync(List<int> knowledgeIds, CancellationToken cancellationToken);

    Task RefreshScenePromptsBySceneIdsAsync(List<int> sceneIds, CancellationToken cancellationToken);

    Task RefreshKnowledgeDetailsByCompanyIdAsync(int companyId, CancellationToken cancellationToken);
}

public class AiSpeechAssistantKnowledgePromptService : IAiSpeechAssistantKnowledgePromptService
{
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IKnowledgeScenarioDataProvider _knowledgeScenarioDataProvider;

    public AiSpeechAssistantKnowledgePromptService(
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        IKnowledgeScenarioDataProvider knowledgeScenarioDataProvider)
    {
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _knowledgeScenarioDataProvider = knowledgeScenarioDataProvider;
    }

    public string BuildFinalPrompt(AiSpeechAssistantKnowledge knowledge)
    {
        if (knowledge == null) return string.Empty;

        var promptSegments = new List<string>();

        if (!string.IsNullOrWhiteSpace(knowledge.Prompt))
            promptSegments.Add(knowledge.Prompt.Trim());

        if (!string.IsNullOrWhiteSpace(knowledge.ScenePrompt))
            promptSegments.Add(knowledge.ScenePrompt.Trim());

        return promptSegments.Count == 0 ? string.Empty : string.Join("\n\n", promptSegments);
    }

    public async Task<string> GenerateScenePromptAsync(int knowledgeId, CancellationToken cancellationToken)
    {
        var relations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsAsync(knowledgeId, cancellationToken).ConfigureAwait(false);
        var sceneIds = relations.Select(x => x.SceneId).Distinct().ToList();

        if (sceneIds.Count == 0)
            return string.Empty;

        var scenes = await _aiSpeechAssistantDataProvider.GetKnowledgeScenesByIdsAsync(sceneIds, cancellationToken).ConfigureAwait(false);
        var publishedScenes = scenes
            .Where(x => x.Status == KnowledgeSceneStatus.Published)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToList();

        if (publishedScenes.Count == 0)
            return string.Empty;

        var sceneKnowledgeMap = (await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdsAsync(publishedScenes.Select(x => x.Id).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false))
            .GroupBy(x => x.SceneId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.UpdatedAt ?? x.CreatedAt).ThenBy(x => x.Id).ToList());

        var sb = new StringBuilder();

        foreach (var scene in publishedScenes)
        {
            if (!sceneKnowledgeMap.TryGetValue(scene.Id, out var sceneKnowledges) || sceneKnowledges.Count == 0)
                continue;

            var sceneContents = sceneKnowledges
                .Select(ResolveSceneKnowledgeContent)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (sceneContents.Count != 0)
            {
                sb.AppendLine("场景知识点：");
                foreach (var content in sceneContents)
                    sb.AppendLine(content);
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public async Task RefreshScenePromptsAsync(List<int> knowledgeIds, CancellationToken cancellationToken)
    {
        var distinctKnowledgeIds = (knowledgeIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (distinctKnowledgeIds.Count == 0)
            return;

        var updates = new List<AiSpeechAssistantKnowledge>();

        foreach (var knowledgeId in distinctKnowledgeIds)
        {
            var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(knowledgeId: knowledgeId, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (knowledge == null)
                continue;

            var scenePrompt = await GenerateScenePromptAsync(knowledgeId, cancellationToken).ConfigureAwait(false);
            if (string.Equals(knowledge.ScenePrompt ?? string.Empty, scenePrompt, StringComparison.Ordinal))
                continue;

            knowledge.ScenePrompt = scenePrompt;
            updates.Add(knowledge);
        }

        if (updates.Count == 0)
            return;

        await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync(updates, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshScenePromptsBySceneIdsAsync(List<int> sceneIds, CancellationToken cancellationToken)
    {
        var distinctSceneIds = (sceneIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (distinctSceneIds.Count == 0)
            return;

        var relations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdsAsync(distinctSceneIds, cancellationToken).ConfigureAwait(false);
        var knowledgeIds = relations.Select(x => x.KnowledgeId).Distinct().ToList();

        await RefreshScenePromptsAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshKnowledgeDetailsByCompanyIdAsync(int companyId, CancellationToken cancellationToken)
    {
        if (companyId <= 0)
            return;

        Log.Information("[KnowledgeDetailSync] Start. CompanyId={CompanyId}", companyId);

        await RepairInactiveSingleKnowledgeForCrmAssistantsAsync(companyId, cancellationToken).ConfigureAwait(false);

        var mappings = await _knowledgeScenarioDataProvider.GetKnowledgeSceneLanguageMappingsAsync(companyId: companyId, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (mappings.Count == 0)
        {
            Log.Information("[KnowledgeDetailSync] Skip. No active mapping. CompanyId={CompanyId}", companyId);
            return;
        }

        var sceneIds = mappings.Select(x => x.SceneId).Where(x => x > 0).Distinct().ToList();
        if (sceneIds.Count == 0)
        {
            Log.Information("[KnowledgeDetailSync] Skip. No valid scene ids. CompanyId={CompanyId}", companyId);
            return;
        }

        var mappingByLanguage = mappings
            .GroupBy(x => x.Language)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.CreatedAt).First().SceneId);

        var assistantKnowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesByCompanyIdAsync(companyId, cancellationToken: cancellationToken).ConfigureAwait(false);
        var crmAssistantKnowledges = assistantKnowledges
            .Where(x => x.SourceSystem == AgentSourceSystem.AiResource && x.KnowledgeId > 0)
            .GroupBy(x => x.KnowledgeId)
            .Select(x => x.First())
            .ToList();
        var crmKnowledgeCount = crmAssistantKnowledges.Count;
        if (crmKnowledgeCount == 0)
        {
            Log.Information("[KnowledgeDetailSync] Skip. No CRM assistant knowledges. CompanyId={CompanyId}", companyId);
            return;
        }

        var mappingCount = mappingByLanguage.Count;
        Log.Information(
            "[KnowledgeDetailSync] Loaded knowledges. CompanyId={CompanyId}, KnowledgeCount={KnowledgeCount}, MappingCount={MappingCount}",
            companyId, crmKnowledgeCount, mappingCount);

        var knowledgeIds = crmAssistantKnowledges.Select(x => x.KnowledgeId).Where(x => x > 0).Distinct().ToList();
        var knowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);
        var knowledgeMap = knowledges.ToDictionary(x => x.Id);
        var existingRelations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsByKnowledgeIdsAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);
        var relationMap = existingRelations
            .GroupBy(x => x.KnowledgeId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var existingDetails = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeDetailsByKnowledgeIdsAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);
        var detailMap = existingDetails
            .GroupBy(x => x.KnowledgeId)
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.CreatedDate).ThenBy(y => y.Id).ToList());
        var mappedSceneIds = mappingByLanguage.Values.Distinct().ToList();
        var sceneItems = mappedSceneIds.Count == 0
            ? []
            : await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdsAsync(mappedSceneIds, cancellationToken: cancellationToken).ConfigureAwait(false);
        var sceneItemsLookup = sceneItems
            .GroupBy(x => x.SceneId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var relationsToAdd = new List<AiSpeechAssistantKnowledgeSceneRelation>();
        var detailsToDelete = new List<AiSpeechAssistantKnowledgeDetail>();
        var affectedKnowledgeIds = new HashSet<int>();

        foreach (var assistantKnowledge in crmAssistantKnowledges)
        {
            var knowledge = await ResolveKnowledgeForRefreshAsync(assistantKnowledge, knowledgeMap, cancellationToken).ConfigureAwait(false);
            if (knowledge == null) continue;

            if (!TryResolveKnowledgeLanguage(knowledge.ModelLanguage, out var language)) continue;
            
            if (!mappingByLanguage.TryGetValue(language.Value, out var sceneId)) continue;
            
            if (!sceneItemsLookup.TryGetValue(sceneId, out var items))
                items = [];
            
            if (items.Count == 0)
            {
                Log.Information(
                    "[KnowledgeDetailSync] Skip. Scene has no items. CompanyId={CompanyId}, AssistantId={AssistantId}, KnowledgeId={KnowledgeId}, SceneId={SceneId}",
                    companyId, assistantKnowledge.AssistantId, knowledge.Id, sceneId);
                continue;
            }

            if (!relationMap.TryGetValue(knowledge.Id, out var relations) || relations.All(x => x.SceneId != sceneId))
            {
                relationsToAdd.Add(new AiSpeechAssistantKnowledgeSceneRelation
                {
                    KnowledgeId = knowledge.Id,
                    SceneId = sceneId,
                    SourceType = AiSpeechAssistantKnowledgeSceneRelationSourceType.CrmAutoSync,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                affectedKnowledgeIds.Add(knowledge.Id);

                Log.Information(
                    "[KnowledgeDetailSync] Prepared new relation. CompanyId={CompanyId}, AssistantId={AssistantId}, KnowledgeId={KnowledgeId}, SceneId={SceneId}",
                    companyId, assistantKnowledge.AssistantId, knowledge.Id, sceneId);
            }

            if (!detailMap.TryGetValue(knowledge.Id, out var details) || !details.Any())
                continue;

            if (!IsExactSceneCopy(details, items))
                continue;

            detailsToDelete.AddRange(details);
            affectedKnowledgeIds.Add(knowledge.Id);

            var detailCount = details.Count;
            Log.Information(
                "[KnowledgeDetailSync] Prepared copied scene details cleanup. CompanyId={CompanyId}, AssistantId={AssistantId}, KnowledgeId={KnowledgeId}, SceneId={SceneId}, DetailCount={DetailCount}",
                companyId, assistantKnowledge.AssistantId, knowledge.Id, sceneId, detailCount);
        }

        Log.Information(
            "[KnowledgeDetailSync] Prepared CRM scene migration. CompanyId={CompanyId}, RelationCount={RelationCount}, DeleteDetailCount={DeleteDetailCount}, ExistingDetailCount={ExistingDetailCount}",
            companyId, relationsToAdd.Count, detailsToDelete.Count, existingDetails.Count);

        if (relationsToAdd.Count > 0)
        {
            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgeSceneRelationsAsync(relationsToAdd, true, cancellationToken).ConfigureAwait(false);
            Log.Information("[KnowledgeDetailSync] Saved relations. CompanyId={CompanyId}, RelationCount={RelationCount}", companyId, relationsToAdd.Count);
        }

        if (detailsToDelete.Count > 0)
        {
            await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantKnowledgeDetailsAsync(detailsToDelete, true, cancellationToken).ConfigureAwait(false);
            Log.Information("[KnowledgeDetailSync] Deleted copied details. CompanyId={CompanyId}, DetailCount={DetailCount}", companyId, detailsToDelete.Count);
        }

        if (affectedKnowledgeIds.Count > 0)
        {
            await RefreshScenePromptsAsync(affectedKnowledgeIds.ToList(), cancellationToken).ConfigureAwait(false);
            Log.Information("[KnowledgeDetailSync] Updated scene prompts. CompanyId={CompanyId}, KnowledgeCount={KnowledgeCount}", companyId, affectedKnowledgeIds.Count);
        }
    }

    private static string GenerateKnowledgePromptForSceneItems(List<KnowledgeSceneItem> items)
    {
        if (items == null || items.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        sb.AppendLine("场景知识点：");
        foreach (var item in items)
        {
            var content = ResolveSceneKnowledgeContent(item);
            if (!string.IsNullOrWhiteSpace(content))
                sb.AppendLine(content);
        }

        return sb.ToString().TrimEnd();
    }

    private static string GetOrBuildScenePrompt(int sceneId, List<KnowledgeSceneItem> items, Dictionary<int, string> promptBySceneId)
    {
        if (promptBySceneId.TryGetValue(sceneId, out var prompt))
            return prompt;

        prompt = GenerateKnowledgePromptForSceneItems(items);
        promptBySceneId[sceneId] = prompt;
        return prompt;
    }

    private static string ResolveSceneKnowledgeContent(KnowledgeSceneItem sceneKnowledge)
    {
        if (sceneKnowledge.Type == KnowledgeSceneItemType.File)
        {
            if (!string.IsNullOrWhiteSpace(sceneKnowledge.FileName))
                return $"文件：{sceneKnowledge.FileName.Trim()}";
        }

        return sceneKnowledge.Content?.Trim() ?? string.Empty;
    }

    private static bool TryResolveKnowledgeLanguage(string modelLanguage, out AutoAddLanguage? language)
    {
        language = null;

        if (string.IsNullOrWhiteSpace(modelLanguage))
            return false;

        if (Enum.TryParse<AutoAddLanguage>(modelLanguage, true, out var parsedLanguage))
        {
            language = parsedLanguage;
            return true;
        }

        return false;
    }

    private async Task RepairInactiveSingleKnowledgeForCrmAssistantsAsync(int companyId, CancellationToken cancellationToken)
    {
        var crmAssistants = await _aiSpeechAssistantDataProvider
            .GetCrmAutoSyncAssistantsInCompanyAsync(companyId, cancellationToken)
            .ConfigureAwait(false);

        if (crmAssistants.Count == 0)
        {
            Log.Information("[KnowledgeDetailSync] CRM repair skip. No CRM assistants. CompanyId={CompanyId}", companyId);
            return;
        }

        Log.Information(
            "[KnowledgeDetailSync] CRM repair scan. CompanyId={CompanyId}, AssistantCount={AssistantCount}",
            companyId, crmAssistants.Select(x => x.AssistantId).Distinct().Count());

        var repairs = new List<AiSpeechAssistantKnowledge>();

        foreach (var crmAssistant in crmAssistants
                     .Where(x => x.AssistantId > 0)
                     .GroupBy(x => x.AssistantId)
                     .Select(x => x.First()))
        {
            var (_, knowledges) = await _aiSpeechAssistantDataProvider
                .GetAiSpeechAssistantKnowledgesAsync(crmAssistant.AssistantId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (knowledges.Count != 1)
            {
                Log.Information(
                    "[KnowledgeDetailSync] CRM repair skip. Assistant has {KnowledgeCount} knowledges. CompanyId={CompanyId}, AssistantId={AssistantId}",
                    knowledges.Count, companyId, crmAssistant.AssistantId);
                continue;
            }

            var onlyKnowledge = knowledges[0];
            if (onlyKnowledge.IsActive)
            {
                Log.Information(
                    "[KnowledgeDetailSync] CRM repair skip. Single knowledge already active. CompanyId={CompanyId}, AssistantId={AssistantId}, KnowledgeId={KnowledgeId}",
                    companyId, crmAssistant.AssistantId, onlyKnowledge.Id);
                continue;
            }

            onlyKnowledge.IsActive = true;
            repairs.Add(onlyKnowledge);

            Log.Warning(
                "[KnowledgeDetailSync] Repair inactive single CRM knowledge. CompanyId={CompanyId}, AssistantId={AssistantId}, KnowledgeId={KnowledgeId}",
                companyId, crmAssistant.AssistantId, onlyKnowledge.Id);
        }

        if (repairs.Count == 0)
            return;

        await _aiSpeechAssistantDataProvider
            .UpdateAiSpeechAssistantKnowledgesAsync(repairs, true, cancellationToken)
            .ConfigureAwait(false);

        Log.Warning(
            "[KnowledgeDetailSync] Repaired inactive single CRM knowledges. CompanyId={CompanyId}, Count={Count}",
            companyId, repairs.Count);
    }

    private async Task<AiSpeechAssistantKnowledge> ResolveKnowledgeForRefreshAsync(
        KnowledgeCopyRelatedInfoDto assistantKnowledge, IReadOnlyDictionary<int, AiSpeechAssistantKnowledge> knowledgeMap, CancellationToken cancellationToken)
    {
        if (knowledgeMap.TryGetValue(assistantKnowledge.KnowledgeId, out var knowledge))
            return knowledge;

        var (_, latestKnowledges) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantKnowledgesAsync(assistantKnowledge.AssistantId, pageIndex: 1, pageSize: 1, cancellationToken: cancellationToken).ConfigureAwait(false);

        knowledge = latestKnowledges.FirstOrDefault();

        return knowledge;
    }

    private static bool IsExactSceneCopy(
        IReadOnlyCollection<AiSpeechAssistantKnowledgeDetail> details,
        IReadOnlyCollection<KnowledgeSceneItem> sceneItems)
    {
        if (details.Count != sceneItems.Count)
            return false;

        var detailKeys = details
            .Select(BuildSceneSyncDetailKey)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var sceneKeys = sceneItems
            .Select(BuildSceneSyncDetailKey)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        return detailKeys.SequenceEqual(sceneKeys, StringComparer.Ordinal);
    }

    private static string BuildSceneSyncDetailKey(AiSpeechAssistantKnowledgeDetail detail)
        => $"{detail.KnowledgeName}|{detail.FormatType}|{detail.Content}|{detail.FileName}";

    private static string BuildSceneSyncDetailKey(KnowledgeSceneItem item)
        => $"{item.Name}|{MapKnowledgeSceneItemType(item.Type)}|{item.Content}|{(string.IsNullOrWhiteSpace(item.FileName) ? null : item.FileName)}";

    private static AiSpeechAssistantKonwledgeFormatType MapKnowledgeSceneItemType(KnowledgeSceneItemType type)
    {
        return type switch
        {
            KnowledgeSceneItemType.FAQ => AiSpeechAssistantKonwledgeFormatType.FAQ,
            KnowledgeSceneItemType.File => AiSpeechAssistantKonwledgeFormatType.FIle,
            _ => AiSpeechAssistantKonwledgeFormatType.Text
        };
    }
}
