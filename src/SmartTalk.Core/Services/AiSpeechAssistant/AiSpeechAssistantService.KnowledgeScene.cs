using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<GetAiSpeechAssistantKnowledgeSceneRelationsResponse> GetAiSpeechAssistantKnowledgeSceneRelationsAsync(GetAiSpeechAssistantKnowledgeSceneRelationsRequest request, CancellationToken cancellationToken);

    Task<AddAiSpeechAssistantKnowledgeSceneRelationResponse> AddAiSpeechAssistantKnowledgeSceneRelationAsync(AddAiSpeechAssistantKnowledgeSceneRelationCommand command, CancellationToken cancellationToken);

    Task<DeleteAiSpeechAssistantKnowledgeSceneRelationResponse> DeleteAiSpeechAssistantKnowledgeSceneRelationAsync(DeleteAiSpeechAssistantKnowledgeSceneRelationCommand command, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<GetAiSpeechAssistantKnowledgeSceneRelationsResponse> GetAiSpeechAssistantKnowledgeSceneRelationsAsync(GetAiSpeechAssistantKnowledgeSceneRelationsRequest request, CancellationToken cancellationToken)
    {
        if (request.KnowledgeId <= 0) throw new Exception("GetAiSpeechAssistantKnowledgeSceneRelations KnowledgeId is required.");

        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(knowledgeId: request.KnowledgeId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (knowledge == null)
            throw new Exception($"GetAiSpeechAssistantKnowledgeSceneRelations Knowledge [{request.KnowledgeId}] does not exist.");

        var relations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsAsync(request.KnowledgeId, cancellationToken).ConfigureAwait(false);
        var relationDtos = _mapper.Map<List<AiSpeechAssistantKnowledgeSceneRelationDto>>(relations);

        if (relations.Count == 0)
            return new GetAiSpeechAssistantKnowledgeSceneRelationsResponse { Data = relationDtos };

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

        return new GetAiSpeechAssistantKnowledgeSceneRelationsResponse
        {
            Data = relationDtos
        };
    }

    public async Task<AddAiSpeechAssistantKnowledgeSceneRelationResponse> AddAiSpeechAssistantKnowledgeSceneRelationAsync(AddAiSpeechAssistantKnowledgeSceneRelationCommand command, CancellationToken cancellationToken)
    {
        if (command.KnowledgeId <= 0) throw new Exception("AddAiSpeechAssistantKnowledgeSceneRelation KnowledgeId is required.");

        if (command.SceneId <= 0) throw new Exception("AddAiSpeechAssistantKnowledgeSceneRelation SceneId is required.");

        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(knowledgeId: command.KnowledgeId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (knowledge == null)
            throw new Exception($"AddAiSpeechAssistantKnowledgeSceneRelation Knowledge [{command.KnowledgeId}] does not exist.");

        var scene = await _aiSpeechAssistantDataProvider.GetKnowledgeSceneByIdAsync(command.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"AddAiSpeechAssistantKnowledgeSceneRelation Scene [{command.SceneId}] does not exist.");

        var duplicatedRelation = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationAsync(command.KnowledgeId, command.SceneId, cancellationToken).ConfigureAwait(false);

        if (duplicatedRelation != null)
            throw new Exception($"AddAiSpeechAssistantKnowledgeSceneRelation Scene [{command.SceneId}] already applied to knowledge [{command.KnowledgeId}].");

        var relation = _mapper.Map<AiSpeechAssistantKnowledgeSceneRelation>(command);

        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgeSceneRelationAsync(relation, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptAsync(command.KnowledgeId, cancellationToken).ConfigureAwait(false);

        var dto = _mapper.Map<AiSpeechAssistantKnowledgeSceneRelationDto>(relation);
        dto.FolderId = scene.FolderId;
        dto.SceneName = scene.Name;
        dto.SceneDescription = scene.Description;
        dto.SceneStatus = scene.Status;

        return new AddAiSpeechAssistantKnowledgeSceneRelationResponse
        {
            Data = dto
        };
    }

    public async Task<DeleteAiSpeechAssistantKnowledgeSceneRelationResponse> DeleteAiSpeechAssistantKnowledgeSceneRelationAsync(DeleteAiSpeechAssistantKnowledgeSceneRelationCommand command, CancellationToken cancellationToken)
    {
        if (command.Id <= 0) throw new Exception("DeleteAiSpeechAssistantKnowledgeSceneRelation Id is required.");

        var relation = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (relation == null)
            throw new Exception($"DeleteAiSpeechAssistantKnowledgeSceneRelation Relation [{command.Id}] does not exist.");

        var scene = await _aiSpeechAssistantDataProvider.GetKnowledgeSceneByIdAsync(relation.SceneId, cancellationToken).ConfigureAwait(false);

        await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantKnowledgeSceneRelationAsync(relation, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptAsync(relation.KnowledgeId, cancellationToken).ConfigureAwait(false);

        var dto = _mapper.Map<AiSpeechAssistantKnowledgeSceneRelationDto>(relation);

        if (scene != null)
        {
            dto.FolderId = scene.FolderId;
            dto.SceneName = scene.Name;
            dto.SceneDescription = scene.Description;
            dto.SceneStatus = scene.Status;
        }

        return new DeleteAiSpeechAssistantKnowledgeSceneRelationResponse
        {
            Data = dto
        };
    }
}
