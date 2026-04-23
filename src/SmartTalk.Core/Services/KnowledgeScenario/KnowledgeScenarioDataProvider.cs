using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.KnowledgeScenario;

public interface IKnowledgeScenarioDataProvider : IScopedDependency
{
    Task<KnowledgeSceneFolder> GetKnowledgeSceneFolderByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<KnowledgeSceneFolder> GetKnowledgeSceneFolderByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<List<KnowledgeSceneFolder>> GetKnowledgeSceneFoldersAsync(string keyword, CancellationToken cancellationToken = default);

    Task AddKnowledgeSceneFolderAsync(KnowledgeSceneFolder folder, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateKnowledgeSceneFolderAsync(KnowledgeSceneFolder folder, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeSceneFolderAsync(KnowledgeSceneFolder folder, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<KnowledgeScene> GetKnowledgeSceneByFolderAndNameAsync(int folderId, string name, CancellationToken cancellationToken = default);

    Task<KnowledgeScene> GetKnowledgeSceneByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<List<KnowledgeScene>> GetKnowledgeScenesByFolderIdAsync(int folderId, CancellationToken cancellationToken = default);

    Task<List<KnowledgeScene>> GetKnowledgeScenesAsync(int folderId, string keyword, CancellationToken cancellationToken = default);

    Task AddKnowledgeSceneAsync(KnowledgeScene scene, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateKnowledgeSceneAsync(KnowledgeScene scene, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeScenesAsync(List<KnowledgeScene> scenes, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<KnowledgeSceneKnowledge> GetKnowledgeSceneKnowledgeByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<KnowledgeSceneKnowledge> GetKnowledgeSceneKnowledgeBySceneAndNameAsync(int sceneId, string name, CancellationToken cancellationToken = default);

    Task<List<KnowledgeSceneKnowledge>> GetKnowledgeSceneKnowledgesAsync(int sceneId, string keyword, CancellationToken cancellationToken = default);

    Task<List<KnowledgeSceneKnowledge>> GetKnowledgeSceneKnowledgesBySceneIdsAsync(List<int> sceneIds, CancellationToken cancellationToken = default);

    Task AddKnowledgeSceneKnowledgeAsync(KnowledgeSceneKnowledge knowledge, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateKnowledgeSceneKnowledgeAsync(KnowledgeSceneKnowledge knowledge, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeSceneKnowledgeAsync(KnowledgeSceneKnowledge knowledge, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeSceneKnowledgesAsync(List<KnowledgeSceneKnowledge> knowledges, bool forceSave = true, CancellationToken cancellationToken = default);
}

public class KnowledgeScenarioDataProvider : IKnowledgeScenarioDataProvider
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public KnowledgeScenarioDataProvider(IRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<KnowledgeSceneFolder> GetKnowledgeSceneFolderByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<KnowledgeSceneFolder>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<KnowledgeSceneFolder> GetKnowledgeSceneFolderByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<KnowledgeSceneFolder>()
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<KnowledgeSceneFolder>> GetKnowledgeSceneFoldersAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<KnowledgeSceneFolder>();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword));

        return await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddKnowledgeSceneFolderAsync(KnowledgeSceneFolder folder, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(folder, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateKnowledgeSceneFolderAsync(KnowledgeSceneFolder folder, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(folder, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteKnowledgeSceneFolderAsync(KnowledgeSceneFolder folder, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(folder, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<KnowledgeScene> GetKnowledgeSceneByFolderAndNameAsync(int folderId, string name, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<KnowledgeScene>()
            .FirstOrDefaultAsync(x => x.FolderId == folderId && x.Name == name, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<KnowledgeScene> GetKnowledgeSceneByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<KnowledgeScene>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<KnowledgeScene>> GetKnowledgeScenesByFolderIdAsync(int folderId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<KnowledgeScene>()
            .Where(x => x.FolderId == folderId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<KnowledgeScene>> GetKnowledgeScenesAsync(int folderId, string keyword, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<KnowledgeScene>().Where(x => x.FolderId == folderId);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword.Trim()));

        return await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddKnowledgeSceneAsync(KnowledgeScene scene, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(scene, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateKnowledgeSceneAsync(KnowledgeScene scene, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(scene, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteKnowledgeScenesAsync(List<KnowledgeScene> scenes, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (scenes.Count != 0)
            await _repository.DeleteAllAsync(scenes, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<KnowledgeSceneKnowledge> GetKnowledgeSceneKnowledgeByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<KnowledgeSceneKnowledge>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<KnowledgeSceneKnowledge> GetKnowledgeSceneKnowledgeBySceneAndNameAsync(int sceneId, string name, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<KnowledgeSceneKnowledge>()
            .FirstOrDefaultAsync(x => x.SceneId == sceneId && x.Name == name, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<KnowledgeSceneKnowledge>> GetKnowledgeSceneKnowledgesAsync(int sceneId, string keyword, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<KnowledgeSceneKnowledge>().Where(x => x.SceneId == sceneId);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword.Trim()));

        return await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<KnowledgeSceneKnowledge>> GetKnowledgeSceneKnowledgesBySceneIdsAsync(List<int> sceneIds, CancellationToken cancellationToken = default)
    {
        if (sceneIds.Count == 0) 
            return new List<KnowledgeSceneKnowledge>();

        return await _repository.Query<KnowledgeSceneKnowledge>().Where(x => sceneIds.Contains(x.SceneId)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddKnowledgeSceneKnowledgeAsync(KnowledgeSceneKnowledge knowledge, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(knowledge, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateKnowledgeSceneKnowledgeAsync(KnowledgeSceneKnowledge knowledge, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(knowledge, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteKnowledgeSceneKnowledgeAsync(KnowledgeSceneKnowledge knowledge, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(knowledge, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteKnowledgeSceneKnowledgesAsync(List<KnowledgeSceneKnowledge> knowledges, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (knowledges.Count != 0)
            await _repository.DeleteAllAsync(knowledges, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
