using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<GetAiSpeechAssistantKnowledgeSceneRelationsResponse> GetAiSpeechAssistantKnowledgeSceneRelationsAsync(GetAiSpeechAssistantKnowledgeSceneRelationsRequest request, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<GetAiSpeechAssistantKnowledgeSceneRelationsResponse> GetAiSpeechAssistantKnowledgeSceneRelationsAsync(GetAiSpeechAssistantKnowledgeSceneRelationsRequest request, CancellationToken cancellationToken)
    {
        if (request.KnowledgeId <= 0) throw new Exception("GetAiSpeechAssistantKnowledgeSceneRelations KnowledgeId is required.");

        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(knowledgeId: request.KnowledgeId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (knowledge == null)
            throw new Exception($"GetAiSpeechAssistantKnowledgeSceneRelations Knowledge [{request.KnowledgeId}] does not exist.");

        var relationDtos = await BuildKnowledgeSceneRelationDtosAsync([request.KnowledgeId], cancellationToken).ConfigureAwait(false);

        return new GetAiSpeechAssistantKnowledgeSceneRelationsResponse
        {
            Data = relationDtos.TryGetValue(request.KnowledgeId, out var data) ? data : []
        };
    }

    private async Task<Dictionary<int, List<AiSpeechAssistantKnowledgeSceneRelationDto>>> BuildKnowledgeSceneRelationDtosAsync(List<int> knowledgeIds, CancellationToken cancellationToken)
    {
        var distinctKnowledgeIds = (knowledgeIds ?? []).Where(x => x > 0).Distinct().ToList();

        if (distinctKnowledgeIds.Count == 0)
            return new Dictionary<int, List<AiSpeechAssistantKnowledgeSceneRelationDto>>();

        var relations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsByKnowledgeIdsAsync(distinctKnowledgeIds, cancellationToken).ConfigureAwait(false);

        if (relations.Count == 0)
            return distinctKnowledgeIds.ToDictionary(x => x, _ => new List<AiSpeechAssistantKnowledgeSceneRelationDto>());

        var relationDtos = _mapper.Map<List<AiSpeechAssistantKnowledgeSceneRelationDto>>(relations);
        var scenes = await _aiSpeechAssistantDataProvider.GetKnowledgeScenesByIdsAsync(relations.Select(x => x.SceneId).Distinct().ToList(), cancellationToken).ConfigureAwait(false);
        var sceneMap = scenes.ToDictionary(x => x.Id);

        relationDtos.ForEach(dto =>
        {
            if (!sceneMap.TryGetValue(dto.SceneId, out var scene))
                return;

            dto.FolderId = scene.FolderId;
            dto.SceneName = scene.Name;
            dto.SceneDescription = scene.Description;
            dto.SceneStatus = scene.Status;
        });

        var result = relationDtos
            .GroupBy(x => x.KnowledgeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var knowledgeId in distinctKnowledgeIds)
            result.TryAdd(knowledgeId, new List<AiSpeechAssistantKnowledgeSceneRelationDto>());

        return result;
    }
}
