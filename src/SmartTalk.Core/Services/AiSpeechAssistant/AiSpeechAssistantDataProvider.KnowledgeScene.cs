using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Domain.Pos;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider
{
    Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetAiSpeechAssistantKnowledgeSceneRelationsAsync(int knowledgeId, CancellationToken cancellationToken = default);

    Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetAiSpeechAssistantKnowledgeSceneRelationsByKnowledgeIdsAsync(List<int> knowledgeIds, CancellationToken cancellationToken = default);

    Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdAsync(int sceneId, CancellationToken cancellationToken = default);

    Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdsAsync(List<int> sceneIds, CancellationToken cancellationToken = default);

    Task AddAiSpeechAssistantKnowledgeSceneRelationsAsync(List<AiSpeechAssistantKnowledgeSceneRelation> relations, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(List<AiSpeechAssistantKnowledgeSceneRelation> relations, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetStoreKnowledgeSceneRelationsBySceneIdAsync(int storeId, int sceneId, CancellationToken cancellationToken = default);

    Task<List<int>> GetStoreActiveKnowledgeIdsByAssistantIdsAsync(int storeId, List<int> assistantIds, CancellationToken cancellationToken = default);

    Task<List<KnowledgeScene>> GetKnowledgeScenesByIdsAsync(List<int> sceneIds, CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider
{
    public async Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetAiSpeechAssistantKnowledgeSceneRelationsAsync(int knowledgeId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AiSpeechAssistantKnowledgeSceneRelation>()
            .Where(x => x.KnowledgeId == knowledgeId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetAiSpeechAssistantKnowledgeSceneRelationsByKnowledgeIdsAsync(List<int> knowledgeIds, CancellationToken cancellationToken = default)
    {
        if (knowledgeIds.Count == 0)
            return new List<AiSpeechAssistantKnowledgeSceneRelation>();

        return await _repository.Query<AiSpeechAssistantKnowledgeSceneRelation>()
            .Where(x => knowledgeIds.Contains(x.KnowledgeId))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdAsync(int sceneId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AiSpeechAssistantKnowledgeSceneRelation>()
            .Where(x => x.SceneId == sceneId)
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

    public async Task AddAiSpeechAssistantKnowledgeSceneRelationsAsync(List<AiSpeechAssistantKnowledgeSceneRelation> relations, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (relations.Count != 0)
            await _repository.InsertAllAsync(relations, cancellationToken).ConfigureAwait(false);

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

    public async Task<List<AiSpeechAssistantKnowledgeSceneRelation>> GetStoreKnowledgeSceneRelationsBySceneIdAsync(int storeId, int sceneId, CancellationToken cancellationToken = default)
    {
        var query =
            from relation in _repository.Query<AiSpeechAssistantKnowledgeSceneRelation>()
            join knowledge in _repository.Query<AiSpeechAssistantKnowledge>() on relation.KnowledgeId equals knowledge.Id
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on knowledge.AssistantId equals assistant.Id
            join agentAssistant in _repository.Query<AgentAssistant>() on assistant.Id equals agentAssistant.AssistantId
            join posAgent in _repository.Query<PosAgent>() on agentAssistant.AgentId equals posAgent.AgentId
            where relation.SceneId == sceneId && posAgent.StoreId == storeId
            select relation;

        return await query.Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<int>> GetStoreActiveKnowledgeIdsByAssistantIdsAsync(int storeId, List<int> assistantIds, CancellationToken cancellationToken = default)
    {
        var query =
            from assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>()
            join agentAssistant in _repository.Query<AgentAssistant>() on assistant.Id equals agentAssistant.AssistantId
            join posAgent in _repository.Query<PosAgent>() on agentAssistant.AgentId equals posAgent.AgentId
            join knowledge in _repository.Query<AiSpeechAssistantKnowledge>() on assistant.Id equals knowledge.AssistantId
            where posAgent.StoreId == storeId && assistantIds.Contains(assistant.Id) && knowledge.IsActive
            select knowledge.Id;

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
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