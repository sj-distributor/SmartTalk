using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.KnowledgeScenario;

namespace SmartTalk.Core.Services.KnowledgeScenario;

public interface IKnowledgeScenarioDataProvider : IScopedDependency
{
    Task<List<KnowledgeSceneFolder>> GetKnowledgeSceneFoldersAsync(int? id = null, string keyword = null, CancellationToken cancellationToken = default);

    Task AddKnowledgeSceneFolderAsync(KnowledgeSceneFolder folder, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateKnowledgeSceneFolderAsync(KnowledgeSceneFolder folder, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeSceneFolderAsync(KnowledgeSceneFolder folder, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<KnowledgeScene>> GetKnowledgeScenesAsync(int? folderId = null, List<int> sceneIds = null, string keyword = null, string name = null, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeScenesAsync(List<KnowledgeScene> scenes, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task AddKnowledgeSceneAsync(KnowledgeScene scene, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateKnowledgeSceneAsync(KnowledgeScene scene, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<(int Count, List<KnowledgeSceneHistory> Histories)> GetKnowledgeSceneHistoriesAsync(int? sceneId = null, int? historyId = null, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
    
    Task AddKnowledgeSceneHistoryAsync(KnowledgeSceneHistory history, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateKnowledgeSceneHistoriesAsync(List<KnowledgeSceneHistory> histories, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeSceneHistoriesAsync(List<KnowledgeSceneHistory> histories, bool forceSave = true, CancellationToken cancellationToken = default);

    Task AddKnowledgeSceneHistoryItemsAsync(List<KnowledgeSceneHistoryItem> items, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<KnowledgeSceneHistoryItem>> GetKnowledgeSceneHistoryItemsAsync(List<int> historyIds, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeSceneHistoryItemsAsync(List<KnowledgeSceneHistoryItem> items, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<KnowledgeSceneItem>> GetKnowledgeSceneItemsAsync(
        int? sceneId = null,
        List<int> sceneIds = null,
        List<string> names = null,
        string keyword = null,
        CancellationToken cancellationToken = default);
    
    Task UpdateKnowledgeSceneItemAsync(KnowledgeSceneItem knowledge, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeSceneItemsAsync(List<KnowledgeSceneItem> knowledges, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task AddKnowledgeSceneItemsAsync(List<KnowledgeSceneItem> knowledges, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateKnowledgeSceneItemsAsync(List<KnowledgeSceneItem> items, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<KnowledgeSceneCompany>> GetKnowledgeSceneCompaniesAsync(List<int> sceneIds = null, int? companyId = null, int? storeId = null, bool? isApplied = null, bool? isCompanyAuthorization = null, CancellationToken cancellationToken = default);

    Task AddKnowledgeSceneCompaniesAsync(List<KnowledgeSceneCompany> knowledgeSceneCompanies, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeleteKnowledgeSceneCompaniesAsync(List<KnowledgeSceneCompany> knowledgeSceneCompanies, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateKnowledgeSceneCompanyAsync(KnowledgeSceneCompany knowledgeSceneCompany, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<AgentKnowledgeDto>> GetAgentKnowledgeAsync(int storeId, string keyword, CancellationToken cancellationToken = default);
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

    public async Task<List<KnowledgeSceneFolder>> GetKnowledgeSceneFoldersAsync(int? id = null, string keyword = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<KnowledgeSceneFolder>();

        if (id.HasValue)
            query = query.Where(x => x.Id == id.Value);

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

    public async Task<List<KnowledgeScene>> GetKnowledgeScenesAsync(int? folderId = null, List<int> sceneIds = null, string keyword = null, string name = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<KnowledgeScene>();

        if (folderId.HasValue)
            query = query.Where(x => x.FolderId == folderId.Value);

        if (sceneIds is { Count: > 0 })
            query = query.Where(x => sceneIds.Contains(x.Id));

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(x => x.Name == name.Trim());

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword.Trim()));

        return await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int Count, List<KnowledgeSceneHistory> Histories)> GetKnowledgeSceneHistoriesAsync(int? sceneId = null, int? historyId = null, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<KnowledgeSceneHistory>();

        if (sceneId.HasValue)
            query = query.Where(x => x.SceneId == sceneId.Value);

        if (historyId.HasValue)
            query = query.Where(x => x.Id == historyId.Value);

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        query = query
            .OrderByDescending(x => x.SnapshotAt)
            .ThenByDescending(x => x.Id);

        if (pageIndex.HasValue && pageSize.HasValue && pageSize > 0)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);

        var histories = await query
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

    public async Task DeleteKnowledgeSceneHistoriesAsync(List<KnowledgeSceneHistory> histories, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (histories.Count != 0)
            await _repository.DeleteAllAsync(histories, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteKnowledgeSceneHistoryItemsAsync(List<KnowledgeSceneHistoryItem> items, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (items.Count != 0)
            await _repository.DeleteAllAsync(items, cancellationToken).ConfigureAwait(false);

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

    public async Task<List<KnowledgeSceneItem>> GetKnowledgeSceneItemsAsync(int? sceneId = null, List<int> sceneIds = null, List<string> names = null, string keyword = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<KnowledgeSceneItem>();

        if (sceneId.HasValue)
            query = query.Where(x => x.SceneId == sceneId.Value);

        if (sceneIds is { Count: > 0 })
            query = query.Where(x => sceneIds.Contains(x.SceneId));

        if (names is { Count: > 0 })
        {
            var distinctNames = names.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
            if (distinctNames.Count == 0)
                return new List<KnowledgeSceneItem>();

            query = query.Where(x => distinctNames.Contains(x.Name));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword.Trim()));

        return await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<KnowledgeSceneCompany>> GetKnowledgeSceneCompaniesAsync(List<int> sceneIds = null, int? companyId = null, int? storeId = null, bool? isApplied = null, bool? isCompanyAuthorization = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<KnowledgeSceneCompany>();

        if (sceneIds is { Count: > 0 })
            query = query.Where(x => sceneIds.Contains(x.SceneId));

        if (companyId.HasValue)
            query = query.Where(x => x.CompanyId == companyId.Value);

        if (storeId.HasValue)
            query = query.Where(x => x.StoreId == storeId.Value);

        if (isApplied.HasValue)
            query = query.Where(x => x.IsApplied == isApplied.Value);

        if (isCompanyAuthorization.HasValue)
            query = isCompanyAuthorization.Value ? query.Where(x => x.StoreId == null) : query.Where(x => x.StoreId != null);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddKnowledgeSceneCompaniesAsync(List<KnowledgeSceneCompany> knowledgeSceneCompanies, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (knowledgeSceneCompanies.Count != 0)
            await _repository.InsertAllAsync(knowledgeSceneCompanies, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteKnowledgeSceneCompaniesAsync(List<KnowledgeSceneCompany> knowledgeSceneCompanies, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (knowledgeSceneCompanies.Count != 0)
            await _repository.DeleteAllAsync(knowledgeSceneCompanies, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AgentKnowledgeDto>> GetAgentKnowledgeAsync(int storeId, string keyword, CancellationToken cancellationToken = default)
    {
        if (storeId <= 0)
            throw new Exception("StoreId is required.");

        var trimmedKeyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();

        var query =
            from store in _repository.QueryNoTracking<CompanyStore>()
            join posAgent in _repository.QueryNoTracking<PosAgent>() on store.Id equals posAgent.StoreId
            join agent in _repository.QueryNoTracking<Agent>() on posAgent.AgentId equals agent.Id 
            join agentAssistant in _repository.QueryNoTracking<AgentAssistant>() on agent.Id equals agentAssistant.AgentId
            join assistant in _repository.QueryNoTracking<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id
            join knowledge in _repository.QueryNoTracking<AiSpeechAssistantKnowledge>() on assistant.Id equals knowledge.AssistantId
            where store.Id == storeId && knowledge.IsActive && agent.IsDisplay && agent.IsSurface
            select new AgentKnowledgeDto
            {
                StoreId = store.Id,
                StoreName = store.Names,
                AgentId = agent.Id,
                AgentName = agent.Name,
                AssistantId = assistant.Id,
                AssistantName = assistant.Name
            };

        if (!string.IsNullOrWhiteSpace(trimmedKeyword))
        {
            query = query.Where(x =>
                x.StoreName.Contains(trimmedKeyword) || x.AgentName.Contains(trimmedKeyword));
        }

        return await query
            .Distinct()
            .OrderBy(x => x.StoreName)
            .ThenBy(x => x.AgentName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
    
    public async Task AddKnowledgeSceneItemsAsync(List<KnowledgeSceneItem> knowledges, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (knowledges.Count != 0)
            await _repository.InsertAllAsync(knowledges, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateKnowledgeSceneCompanyAsync(KnowledgeSceneCompany knowledgeSceneCompany, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(knowledgeSceneCompany, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateKnowledgeSceneItemAsync(KnowledgeSceneItem knowledge, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(knowledge, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateKnowledgeSceneItemsAsync(List<KnowledgeSceneItem> items, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (items is { Count: > 0 })
            await _repository.UpdateAllAsync(items, cancellationToken).ConfigureAwait(false);

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
