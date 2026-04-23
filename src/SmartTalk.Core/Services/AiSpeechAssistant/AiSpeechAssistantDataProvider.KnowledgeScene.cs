using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider
{
    Task<KnowledgeScene> GetKnowledgeSceneByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantKnowledgeSceneRelation> GetAiSpeechAssistantKnowledgeSceneRelationByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantKnowledgeSceneRelation> GetAiSpeechAssistantKnowledgeSceneRelationAsync(int knowledgeId, int sceneId, CancellationToken cancellationToken = default);

    Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetAiSpeechAssistantKnowledgeSceneRelationsAsync(int knowledgeId, CancellationToken cancellationToken = default);

    Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdsAsync(List<int> sceneIds, CancellationToken cancellationToken = default);

    Task AddAiSpeechAssistantKnowledgeSceneRelationAsync(AiSpeechAssistantKnowledgeSceneRelation relation, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteAiSpeechAssistantKnowledgeSceneRelationAsync(AiSpeechAssistantKnowledgeSceneRelation relation, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(List<AiSpeechAssistantKnowledgeSceneRelation> relations, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<KnowledgeScene>> GetKnowledgeScenesByIdsAsync(List<int> sceneIds, CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider
{
    public async Task<KnowledgeScene> GetKnowledgeSceneByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<KnowledgeScene>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantKnowledgeSceneRelation> GetAiSpeechAssistantKnowledgeSceneRelationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AiSpeechAssistantKnowledgeSceneRelation>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantKnowledgeSceneRelation> GetAiSpeechAssistantKnowledgeSceneRelationAsync(int knowledgeId, int sceneId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AiSpeechAssistantKnowledgeSceneRelation>()
            .FirstOrDefaultAsync(x => x.KnowledgeId == knowledgeId && x.SceneId == sceneId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetAiSpeechAssistantKnowledgeSceneRelationsAsync(int knowledgeId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AiSpeechAssistantKnowledgeSceneRelation>()
            .Where(x => x.KnowledgeId == knowledgeId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdsAsync(List<int> sceneIds, CancellationToken cancellationToken = default)
    {
        if (sceneIds.Count == 0)
            return new List<AiSpeechAssistantKnowledgeSceneRelation>();

        return await _repository.Query<AiSpeechAssistantKnowledgeSceneRelation>()
            .Where(x => sceneIds.Contains(x.SceneId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAiSpeechAssistantKnowledgeSceneRelationAsync(AiSpeechAssistantKnowledgeSceneRelation relation, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(relation, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAiSpeechAssistantKnowledgeSceneRelationAsync(AiSpeechAssistantKnowledgeSceneRelation relation, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(relation, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(List<AiSpeechAssistantKnowledgeSceneRelation> relations, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (relations.Count != 0)
            await _repository.DeleteAllAsync(relations, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<KnowledgeScene>> GetKnowledgeScenesByIdsAsync(List<int> sceneIds, CancellationToken cancellationToken = default)
    {
        if (sceneIds.Count == 0)
            return new List<KnowledgeScene>();

        return await _repository.Query<KnowledgeScene>()
            .Where(x => sceneIds.Contains(x.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
