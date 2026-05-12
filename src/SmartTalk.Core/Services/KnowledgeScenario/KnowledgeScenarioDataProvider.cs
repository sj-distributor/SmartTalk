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

    Task<KnowledgeSceneHistory> GetKnowledgeSceneHistoryByIdAsync(int historyId, CancellationToken cancellationToken = default);

    Task<List<KnowledgeScene>> GetKnowledgeScenesByFolderIdAsync(int folderId, CancellationToken cancellationToken = default);

    Task<List<KnowledgeScene>> GetKnowledgeScenesAsync(int folderId, string keyword, CancellationToken cancellationToken = default);

    Task<(int, List<KnowledgeSceneHistory>)> GetKnowledgeSceneHistoriesAsync(int sceneId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);

    Task<List<KnowledgeSceneHistoryItem>> GetKnowledgeSceneHistoryItemsAsync(List<int> historyIds, CancellationToken cancellationToken = default);

    Task AddKnowledgeSceneAsync(KnowledgeScene scene, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateKnowledgeSceneAsync(KnowledgeScene scene, bool forceSave = true, CancellationToken cancellationToken = default);

    Task AddKnowledgeSceneHistoryAsync(KnowledgeSceneHistory history, bool forceSave = true, CancellationToken cancellationToken = default);

    Task AddKnowledgeSceneHistoryItemsAsync(List<KnowledgeSceneHistoryItem> items, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateKnowledgeSceneHistoriesAsync(List<KnowledgeSceneHistory> histories, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeScenesAsync(List<KnowledgeScene> scenes, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<KnowledgeSceneItem> GetKnowledgeSceneItemByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<KnowledgeSceneItem> GetKnowledgeSceneItemBySceneAndNameAsync(int sceneId, string name, CancellationToken cancellationToken = default);

    Task<List<KnowledgeSceneItem>> GetKnowledgeSceneItemsBySceneAndNamesAsync(int sceneId, List<string> names, CancellationToken cancellationToken = default);

    Task<List<KnowledgeSceneItem>> GetKnowledgeSceneItemsAsync(int sceneId, string keyword, CancellationToken cancellationToken = default);

    Task<List<KnowledgeSceneItem>> GetKnowledgeSceneItemsBySceneIdsAsync(List<int> sceneIds, CancellationToken cancellationToken = default);

    Task AddKnowledgeSceneItemAsync(KnowledgeSceneItem knowledge, bool forceSave = true, CancellationToken cancellationToken = default);

    Task AddKnowledgeSceneItemsAsync(List<KnowledgeSceneItem> knowledges, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateKnowledgeSceneItemAsync(KnowledgeSceneItem knowledge, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeSceneItemAsync(KnowledgeSceneItem knowledge, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeSceneItemsAsync(List<KnowledgeSceneItem> knowledges, bool forceSave = true, CancellationToken cancellationToken = default);
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

    public async Task<KnowledgeSceneHistory> GetKnowledgeSceneHistoryByIdAsync(int historyId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<KnowledgeSceneHistory>()
            .FirstOrDefaultAsync(x => x.Id == historyId, cancellationToken)
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

    public async Task<(int, List<KnowledgeSceneHistory>)> GetKnowledgeSceneHistoriesAsync(int sceneId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<KnowledgeSceneHistory>().Where(x => x.SceneId == sceneId);
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue && pageIndex >= 0 && pageSize > 0)
            query = query.Skip(pageIndex.Value * pageSize.Value).Take(pageSize.Value);

        var histories = await query
            .OrderByDescending(x => x.SnapshotAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (count, histories);
    }

    public async Task<List<KnowledgeSceneHistoryItem>> GetKnowledgeSceneHistoryItemsAsync(List<int> historyIds, CancellationToken cancellationToken = default)
    {
        if (historyIds.Count == 0)
            return new List<KnowledgeSceneHistoryItem>();

        return await _repository.Query<KnowledgeSceneHistoryItem>()
            .Where(x => historyIds.Contains(x.HistoryId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
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

    public async Task AddKnowledgeSceneHistoryAsync(KnowledgeSceneHistory history, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(history, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddKnowledgeSceneHistoryItemsAsync(List<KnowledgeSceneHistoryItem> items, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (items.Count != 0)
            await _repository.InsertAllAsync(items, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateKnowledgeSceneHistoriesAsync(List<KnowledgeSceneHistory> histories, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (histories.Count != 0)
            await _repository.UpdateAllAsync(histories, cancellationToken).ConfigureAwait(false);

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

    public async Task<KnowledgeSceneItem> GetKnowledgeSceneItemByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<KnowledgeSceneItem>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<KnowledgeSceneItem> GetKnowledgeSceneItemBySceneAndNameAsync(int sceneId, string name, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<KnowledgeSceneItem>()
            .FirstOrDefaultAsync(x => x.SceneId == sceneId && x.Name == name, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<KnowledgeSceneItem>> GetKnowledgeSceneItemsBySceneAndNamesAsync(int sceneId, List<string> names, CancellationToken cancellationToken = default)
    {
        if (names.Count == 0)
            return new List<KnowledgeSceneItem>();

        var distinctNames = names.Distinct().ToList();

        return await _repository.Query<KnowledgeSceneItem>()
            .Where(x => x.SceneId == sceneId && distinctNames.Contains(x.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<KnowledgeSceneItem>> GetKnowledgeSceneItemsAsync(int sceneId, string keyword, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<KnowledgeSceneItem>().Where(x => x.SceneId == sceneId);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword.Trim()));

        return await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<KnowledgeSceneItem>> GetKnowledgeSceneItemsBySceneIdsAsync(List<int> sceneIds, CancellationToken cancellationToken = default)
    {
        if (sceneIds.Count == 0) 
            return new List<KnowledgeSceneItem>();

        return await _repository.Query<KnowledgeSceneItem>().Where(x => sceneIds.Contains(x.SceneId)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddKnowledgeSceneItemAsync(KnowledgeSceneItem knowledge, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(knowledge, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddKnowledgeSceneItemsAsync(List<KnowledgeSceneItem> knowledges, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (knowledges.Count != 0)
            await _repository.InsertAllAsync(knowledges, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateKnowledgeSceneItemAsync(KnowledgeSceneItem knowledge, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(knowledge, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteKnowledgeSceneItemAsync(KnowledgeSceneItem knowledge, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(knowledge, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteKnowledgeSceneItemsAsync(List<KnowledgeSceneItem> knowledges, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (knowledges.Count != 0)
            await _repository.DeleteAllAsync(knowledges, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
