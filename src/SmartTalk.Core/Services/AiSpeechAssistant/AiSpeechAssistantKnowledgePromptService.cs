using System.Text;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public interface IAiSpeechAssistantKnowledgePromptService : IScopedDependency
{
    string BuildFinalPrompt(AiSpeechAssistantKnowledge knowledge);

    Task<string> GenerateScenePromptAsync(int knowledgeId, CancellationToken cancellationToken);

    Task RefreshScenePromptsAsync(List<int> knowledgeIds, CancellationToken cancellationToken);

    Task RefreshScenePromptsBySceneIdsAsync(List<int> sceneIds, CancellationToken cancellationToken);
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

        var sceneKnowledgeMap = (await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdsAsync(publishedScenes.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false))
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

    private static string ResolveSceneKnowledgeContent(KnowledgeSceneItem sceneKnowledge)
    {
        if (sceneKnowledge.Type == KnowledgeSceneItemType.File)
        {
            if (!string.IsNullOrWhiteSpace(sceneKnowledge.FileName))
                return $"文件：{sceneKnowledge.FileName.Trim()}";
        }

        return sceneKnowledge.Content?.Trim() ?? string.Empty;
    }
}
