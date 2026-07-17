using System.Text;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.KnowledgeScenario;
using Serilog;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
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

    private static AiSpeechAssistantKnowledgeDetail BuildKnowledgeDetail(int knowledgeId, KnowledgeSceneItem item)
    {
        return new AiSpeechAssistantKnowledgeDetail
        {
            KnowledgeId = knowledgeId,
            KnowledgeName = item.Name,
            FormatType = MapKnowledgeSceneItemType(item.Type),
            Content = item.Content,
            FileName = string.IsNullOrWhiteSpace(item.FileName) ? null : item.FileName,
            SourceType = "scene",
            SourceSceneId = item.SceneId,
            SourceSceneItemId = item.Id,
            CreatedDate = DateTimeOffset.UtcNow
        };
    }

    private static bool IsSceneGeneratedDetail(AiSpeechAssistantKnowledgeDetail detail)
    {
        return string.Equals(detail.SourceType, "scene", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NeedsSceneDetailUpdate(AiSpeechAssistantKnowledgeDetail detail, KnowledgeSceneItem item)
    {
        return !string.Equals(detail.KnowledgeName ?? string.Empty, item.Name ?? string.Empty, StringComparison.Ordinal)
               || detail.FormatType != MapKnowledgeSceneItemType(item.Type)
               || !string.Equals(detail.Content ?? string.Empty, item.Content ?? string.Empty, StringComparison.Ordinal)
               || !string.Equals(detail.FileName ?? string.Empty, item.FileName ?? string.Empty, StringComparison.Ordinal)
               || detail.SourceSceneId != item.SceneId
               || detail.SourceSceneItemId != item.Id
               || !string.Equals(detail.SourceType ?? string.Empty, "scene", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplySceneDetail(AiSpeechAssistantKnowledgeDetail detail, KnowledgeSceneItem item)
    {
        detail.KnowledgeName = item.Name;
        detail.FormatType = MapKnowledgeSceneItemType(item.Type);
        detail.Content = item.Content;
        detail.FileName = string.IsNullOrWhiteSpace(item.FileName) ? null : item.FileName;
        detail.SourceType = "scene";
        detail.SourceSceneId = item.SceneId;
        detail.SourceSceneItemId = item.Id;
    }

    private static AiSpeechAssistantKonwledgeFormatType MapKnowledgeSceneItemType(KnowledgeSceneItemType type)
    {
        return type == KnowledgeSceneItemType.File
            ? AiSpeechAssistantKonwledgeFormatType.FIle
            : type == KnowledgeSceneItemType.FAQ
                ? AiSpeechAssistantKonwledgeFormatType.FAQ
                : AiSpeechAssistantKonwledgeFormatType.Text;
    }
    
    public async Task RefreshKnowledgeDetailsByCompanyIdAsync(int companyId, CancellationToken cancellationToken)
    {
        if (companyId <= 0)
            return;

        Log.Information("[KnowledgeDetailSync] Start. CompanyId={CompanyId}", companyId);

        var setup = await BuildKnowledgeDetailRefreshContextAsync(companyId, cancellationToken).ConfigureAwait(false);
        if (setup == null)
            return;

        var (refreshTargets, existingDetailCount, context) = setup.Value;

        foreach (var refreshTarget in refreshTargets)
            PrepareKnowledgeRefresh(companyId, context, refreshTarget);

        await SaveKnowledgeRefreshChangesAsync(companyId, existingDetailCount, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(List<KnowledgeRefreshTarget> RefreshTargets, int ExistingDetailCount, KnowledgeDetailRefreshContext Context)?> BuildKnowledgeDetailRefreshContextAsync(int companyId, CancellationToken cancellationToken)
    {
        var mappings = await _knowledgeScenarioDataProvider.GetKnowledgeSceneLanguageMappingsAsync(companyId: companyId, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (mappings.Count == 0)
        {
            Log.Information("[KnowledgeDetailSync] Skip. No active mapping. CompanyId={CompanyId}", companyId);
            return null;
        }

        var mappingByLanguage = mappings
            .GroupBy(x => x.Language)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.CreatedAt).First().SceneId);

        var assistantKnowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesByCompanyIdAsync(companyId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var knowledgeIds = assistantKnowledges.Select(x => x.KnowledgeId).Where(x => x > 0).Distinct().ToList();
        var knowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);
       
        var existingDetails = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeDetailsByKnowledgeIdsAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);
       
        var sceneItems = await LoadSceneItemsAsync(mappingByLanguage.Values.Distinct().ToList(), cancellationToken).ConfigureAwait(false);
       
        var crmAssistantIds = (await _aiSpeechAssistantDataProvider.GetCrmAutoSyncAssistantsInCompanyAsync(companyId, cancellationToken).ConfigureAwait(false))
            .Select(x => x.AssistantId)
            .ToHashSet();
        
        var refreshTargets = await BuildKnowledgeRefreshTargetsAsync(assistantKnowledges, knowledges, cancellationToken).ConfigureAwait(false);
       
        var singleKnowledgeCrmAssistantIds = assistantKnowledges
            .Where(x => x.AssistantId > 0 && crmAssistantIds.Contains(x.AssistantId))
            .GroupBy(x => x.AssistantId)
            .Where(x => x.Select(y => y.KnowledgeId).Where(y => y > 0).Distinct().Count() == 1)
            .Select(x => x.Key)
            .ToHashSet();

        Log.Information(
            "[KnowledgeDetailSync] Loaded knowledges. CompanyId={CompanyId}, KnowledgeCount={KnowledgeCount}, MappingCount={MappingCount}",
            companyId, assistantKnowledges.Count, mappingByLanguage.Count);

        return (refreshTargets, existingDetails.Count, new KnowledgeDetailRefreshContext
        {
            MappingByLanguage = mappingByLanguage,
            KnowledgeMap = knowledges.ToDictionary(x => x.Id),
            ExistingDetailsByKnowledgeId = existingDetails.GroupBy(x => x.KnowledgeId).ToDictionary(x => x.Key, x => x.ToList()),
            SceneItemsLookup = sceneItems.GroupBy(x => x.SceneId).ToDictionary(x => x.Key, x => x.ToList()),
            SingleKnowledgeCrmAssistantIds = singleKnowledgeCrmAssistantIds
        });
    }

    private async Task<List<KnowledgeSceneItem>> LoadSceneItemsAsync(List<int> sceneIds, CancellationToken cancellationToken)
    {
        if (sceneIds.Count == 0)
            return [];

        return await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdsAsync(sceneIds, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<KnowledgeRefreshTarget>> BuildKnowledgeRefreshTargetsAsync(List<KnowledgeCopyRelatedInfoDto> assistantKnowledges, List<AiSpeechAssistantKnowledge> knowledges, CancellationToken cancellationToken)
    {
        var knowledgeMap = knowledges.ToDictionary(x => x.Id);
        var missingAssistantIds = assistantKnowledges
            .Where(x => !knowledgeMap.ContainsKey(x.KnowledgeId) && x.AssistantId > 0)
            .Select(x => x.AssistantId)
            .Distinct()
            .ToList();

        var fallbackKnowledgeByAssistantId = new Dictionary<int, AiSpeechAssistantKnowledge>();
        foreach (var assistantId in missingAssistantIds)
        {
            var (_, latestKnowledges) = await _aiSpeechAssistantDataProvider
                .GetAiSpeechAssistantKnowledgesAsync(assistantId, pageIndex: 1, pageSize: 1, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var latestKnowledge = latestKnowledges.FirstOrDefault();
            if (latestKnowledge != null)
                fallbackKnowledgeByAssistantId[assistantId] = latestKnowledge;
        }

        return assistantKnowledges
            .Select(x =>
            {
                knowledgeMap.TryGetValue(x.KnowledgeId, out var knowledge);
                if (knowledge == null)
                    fallbackKnowledgeByAssistantId.TryGetValue(x.AssistantId, out knowledge);

                return knowledge == null ? null : new KnowledgeRefreshTarget(x, knowledge);
            })
            .Where(x => x != null)
            .ToList();
    }

    private void PrepareKnowledgeRefresh(
        int companyId, KnowledgeDetailRefreshContext context, KnowledgeRefreshTarget refreshTarget)
    {
        var assistantKnowledge = refreshTarget.AssistantKnowledge;
        var knowledge = refreshTarget.Knowledge;

        var isCrmAssistant = TryRepairInactiveSingleKnowledgeForCrmAssistant(
            companyId, assistantKnowledge.AssistantId, knowledge, context.SingleKnowledgeCrmAssistantIds);

        if (!TryResolveKnowledgeLanguage(knowledge.ModelLanguage, out var language))
            return;

        if (!context.MappingByLanguage.TryGetValue(language.Value, out var sceneId))
            return;

        if (!context.SceneItemsLookup.TryGetValue(sceneId, out var items) || items.Count == 0)
        {
            Log.Information(
                "[KnowledgeDetailSync] Skip. Scene has no items. CompanyId={CompanyId}, AssistantId={AssistantId}, KnowledgeId={KnowledgeId}, SceneId={SceneId}",
                companyId, assistantKnowledge.AssistantId, knowledge.Id, sceneId);
            return;
        }

        context.ExistingDetailsByKnowledgeId.TryGetValue(knowledge.Id, out var knowledgeExistingDetails);
        knowledgeExistingDetails ??= [];

        SyncSceneGeneratedDetails(
            companyId, context, assistantKnowledge.AssistantId, knowledge.Id, sceneId, items, knowledgeExistingDetails, isCrmAssistant);

        var scenePrompt = GetOrBuildScenePrompt(sceneId, items, context.PromptBySceneId);
        knowledge.Prompt = scenePrompt;
        context.KnowledgeUpdates.TryAdd(knowledge.Id, knowledge);

        Log.Information(
            "[KnowledgeDetailSync] Prepared prompt update. CompanyId={CompanyId}, AssistantId={AssistantId}, KnowledgeId={KnowledgeId}, SceneId={SceneId}, SceneItemCount={SceneItemCount}",
            companyId, assistantKnowledge.AssistantId, knowledge.Id, sceneId, items.Count);
    }

    private async Task SaveKnowledgeRefreshChangesAsync(int companyId, int existingDetailCount, KnowledgeDetailRefreshContext context, CancellationToken cancellationToken)
    {
        Log.Information(
            "[KnowledgeDetailSync] Prepared details. CompanyId={CompanyId}, DetailCount={DetailCount}, SceneCount={SceneCount}, ExistingDetailCount={ExistingDetailCount}",
            companyId, context.DetailsToAdd.Count, context.PromptBySceneId.Count, existingDetailCount);

        if (context.DetailsToAdd.Count > 0)
        {
            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgeDetailsAsync(context.DetailsToAdd, true, cancellationToken).ConfigureAwait(false);
            Log.Information("[KnowledgeDetailSync] Saved details. CompanyId={CompanyId}, DetailCount={DetailCount}", companyId, context.DetailsToAdd.Count);
        }

        if (context.DetailsToUpdate.Count > 0)
        {
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgeDetailsAsync(context.DetailsToUpdate, true, cancellationToken).ConfigureAwait(false);
            Log.Information("[KnowledgeDetailSync] Updated details. CompanyId={CompanyId}, DetailCount={DetailCount}", companyId, context.DetailsToUpdate.Count);
        }

        if (context.DetailsToDelete.Count > 0)
        {
            await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantKnowledgeDetailsAsync(context.DetailsToDelete, true, cancellationToken).ConfigureAwait(false);
            Log.Information("[KnowledgeDetailSync] Deleted stale details. CompanyId={CompanyId}, DetailCount={DetailCount}", companyId, context.DetailsToDelete.Count);
        }

        if (context.DetailsToAdd.Count == 0 && context.DetailsToUpdate.Count == 0 && context.DetailsToDelete.Count == 0)
            Log.Information("[KnowledgeDetailSync] Nothing to save. CompanyId={CompanyId}", companyId);

        if (context.KnowledgeUpdates.Count > 0)
        {
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync(context.KnowledgeUpdates.Values.ToList(), true, cancellationToken).ConfigureAwait(false);
            Log.Information("[KnowledgeDetailSync] Updated prompts. CompanyId={CompanyId}, KnowledgeCount={KnowledgeCount}", companyId, context.KnowledgeUpdates.Count);
        }
    }

    private static bool TryRepairInactiveSingleKnowledgeForCrmAssistant(
        int companyId, int assistantId, AiSpeechAssistantKnowledge knowledge, IReadOnlySet<int> singleKnowledgeCrmAssistantIds)
    {
        var isCrmAssistant = singleKnowledgeCrmAssistantIds.Contains(assistantId);
        if (!isCrmAssistant)
            return false;

        if (knowledge.IsActive)
            return true;

        knowledge.IsActive = true;
        Log.Warning(
            "[KnowledgeDetailSync] Repair inactive single CRM knowledge inline. CompanyId={CompanyId}, AssistantId={AssistantId}, KnowledgeId={KnowledgeId}",
            companyId, assistantId, knowledge.Id);

        return true;
    }
    
    private void SyncSceneGeneratedDetails(
        int companyId, KnowledgeDetailRefreshContext context, int assistantId, int knowledgeId, int sceneId,
        List<KnowledgeSceneItem> items, List<AiSpeechAssistantKnowledgeDetail> existingDetails, bool isCrmAssistant)
    {
        var existingSceneDetails = existingDetails
            .Where(IsSceneGeneratedDetail)
            .ToList();

        var hasLegacyUntypedDetails = existingDetails.Count > 0 && existingSceneDetails.Count == 0;
        if (isCrmAssistant && hasLegacyUntypedDetails)
        {
            context.DetailsToDelete.AddRange(existingDetails);
            context.DetailsToAdd.AddRange(items.Select(item => BuildKnowledgeDetail(knowledgeId, item)));

            Log.Warning(
                "[KnowledgeDetailSync] Rebuilt legacy CRM details with scene metadata. CompanyId={CompanyId}, AssistantId={AssistantId}, KnowledgeId={KnowledgeId}, SceneId={SceneId}, ExistingDetailCount={ExistingDetailCount}, NewDetailCount={NewDetailCount}",
                companyId, assistantId, knowledgeId, sceneId, existingDetails.Count, items.Count);
            return;
        }

        var existingSceneDetailMap = existingSceneDetails
            .Where(x => x.SourceSceneItemId.HasValue)
            .ToDictionary(x => x.SourceSceneItemId!.Value, x => x);

        context.DetailsToAdd.AddRange(items
            .Where(item => !existingSceneDetailMap.ContainsKey(item.Id))
            .Select(item => BuildKnowledgeDetail(knowledgeId, item)));

        var detailsToRefresh = items
            .Where(item => existingSceneDetailMap.TryGetValue(item.Id, out var existingSceneDetail) && NeedsSceneDetailUpdate(existingSceneDetail, item))
            .Select(item => (Item: item, Detail: existingSceneDetailMap[item.Id]))
            .ToList();

        detailsToRefresh.ForEach(x =>
        {
            ApplySceneDetail(x.Detail, x.Item);
            x.Detail.LastModifiedDate = DateTimeOffset.UtcNow;
        });
        context.DetailsToUpdate.AddRange(detailsToRefresh.Select(x => x.Detail));

        var desiredSceneItemIds = items.Select(x => x.Id).ToHashSet();
        context.DetailsToDelete.AddRange(existingSceneDetails.Where(x => !x.SourceSceneItemId.HasValue || !desiredSceneItemIds.Contains(x.SourceSceneItemId.Value)));
    }
    
    private sealed class KnowledgeDetailRefreshContext
    {
        public required Dictionary<AutoAddLanguage, int> MappingByLanguage { get; init; }
        public required Dictionary<int, AiSpeechAssistantKnowledge> KnowledgeMap { get; init; }
        public required Dictionary<int, List<AiSpeechAssistantKnowledgeDetail>> ExistingDetailsByKnowledgeId { get; init; }
        public required Dictionary<int, List<KnowledgeSceneItem>> SceneItemsLookup { get; init; }
        public required HashSet<int> SingleKnowledgeCrmAssistantIds { get; init; }
        public List<AiSpeechAssistantKnowledgeDetail> DetailsToAdd { get; init; } = [];
        public List<AiSpeechAssistantKnowledgeDetail> DetailsToUpdate { get; init; } = [];
        public List<AiSpeechAssistantKnowledgeDetail> DetailsToDelete { get; init; } = [];
        public Dictionary<int, string> PromptBySceneId { get; init; } = [];
        public Dictionary<int, AiSpeechAssistantKnowledge> KnowledgeUpdates { get; init; } = [];
    }

    private sealed record KnowledgeRefreshTarget(
        KnowledgeCopyRelatedInfoDto AssistantKnowledge,
        AiSpeechAssistantKnowledge Knowledge);
}
