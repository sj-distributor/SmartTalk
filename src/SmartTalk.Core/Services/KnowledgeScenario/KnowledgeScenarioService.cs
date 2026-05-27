using AutoMapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Ioc;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Messages.Commands.KnowledgeScenario;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Services.KnowledgeScenario;

public interface IKnowledgeScenarioService : IScopedDependency
{
    Task<AddKnowledgeSceneFolderResponse> AddKnowledgeSceneFolderAsync(AddKnowledgeSceneFolderCommand command, CancellationToken cancellationToken);

    Task<UpdateKnowledgeSceneFolderResponse> UpdateKnowledgeSceneFolderAsync(UpdateKnowledgeSceneFolderCommand command, CancellationToken cancellationToken);

    Task<DeleteKnowledgeSceneFolderResponse> DeleteKnowledgeSceneFolderAsync(DeleteKnowledgeSceneFolderCommand command, CancellationToken cancellationToken);

    Task<AddKnowledgeSceneResponse> AddKnowledgeSceneAsync(AddKnowledgeSceneCommand command, CancellationToken cancellationToken);

    Task<UpdateKnowledgeSceneResponse> UpdateKnowledgeSceneAsync(UpdateKnowledgeSceneCommand command, CancellationToken cancellationToken);

    Task<DeleteKnowledgeSceneResponse> DeleteKnowledgeSceneAsync(DeleteKnowledgeSceneCommand command, CancellationToken cancellationToken);
    
    Task<GetKnowledgeSceneFoldersResponse> GetKnowledgeSceneFoldersAsync(GetKnowledgeSceneFoldersRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeScenesResponse> GetKnowledgeScenesAsync(GetKnowledgeScenesRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneResponse> GetKnowledgeSceneAsync(GetKnowledgeSceneRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneItemsResponse> GetKnowledgeSceneItemsAsync(GetKnowledgeSceneItemsRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneHistoryResponse> GetKnowledgeSceneHistoryAsync(GetKnowledgeSceneHistoryRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneMarketResponse> GetKnowledgeSceneMarketAsync(GetKnowledgeSceneMarketRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneCompaniesResponse> GetKnowledgeSceneCompaniesAsync(GetKnowledgeSceneCompaniesRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneRelatedKnowledgesResponse> GetKnowledgeSceneRelatedKnowledgesAsync(GetKnowledgeSceneRelatedKnowledgesRequest request, CancellationToken cancellationToken);

    Task<SaveKnowledgeSceneRelatedKnowledgesResponse> SaveKnowledgeSceneRelatedKnowledgesAsync(SaveKnowledgeSceneRelatedKnowledgesCommand command, CancellationToken cancellationToken);

    Task<SaveKnowledgeSceneCompaniesResponse> SaveKnowledgeSceneCompaniesAsync(SaveKnowledgeSceneCompaniesCommand command, CancellationToken cancellationToken);

    Task<SwitchKnowledgeSceneVersionResponse> SwitchKnowledgeSceneVersionAsync(SwitchKnowledgeSceneVersionCommand command, CancellationToken cancellationToken);

    Task<UpdateKnowledgeSceneCompanyResponse> UpdateKnowledgeSceneCompanyAsync(UpdateKnowledgeSceneCompanyCommand command, CancellationToken cancellationToken);

    Task<UpdateKnowledgeSceneHistoryResponse> UpdateKnowledgeSceneHistoryAsync(UpdateKnowledgeSceneHistoryCommand command, CancellationToken cancellationToken);

    Task<GetAgentKnowledgeResponse> GetAgentKnowledgeAsync(GetAgentKnowledgeRequest request, CancellationToken cancellationToken);
}

public class KnowledgeScenarioService : IKnowledgeScenarioService
{
    private readonly IMapper _mapper;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IAiSpeechAssistantKnowledgePromptService _aiSpeechAssistantKnowledgePromptService;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IKnowledgeScenarioDataProvider _knowledgeScenarioDataProvider;

    public KnowledgeScenarioService(
        IMapper mapper,
        IKnowledgeScenarioDataProvider knowledgeScenarioDataProvider,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        IPosDataProvider posDataProvider,
        ISmartiesClient smartiesClient,
        IAiSpeechAssistantKnowledgePromptService aiSpeechAssistantKnowledgePromptService)
    {
        _mapper = mapper;
        _knowledgeScenarioDataProvider = knowledgeScenarioDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _posDataProvider = posDataProvider;
        _smartiesClient = smartiesClient;
        _aiSpeechAssistantKnowledgePromptService = aiSpeechAssistantKnowledgePromptService;
    }

    public async Task<AddKnowledgeSceneFolderResponse> AddKnowledgeSceneFolderAsync(AddKnowledgeSceneFolderCommand command, CancellationToken cancellationToken)
    {
        var folderName = string.IsNullOrWhiteSpace(command.Name) ? "New Folder" : command.Name;

        var folder = new KnowledgeSceneFolder
        {
            Name = folderName
        };

        await _knowledgeScenarioDataProvider.AddKnowledgeSceneFolderAsync(folder, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AddKnowledgeSceneFolderResponse
        {
            Data = _mapper.Map<KnowledgeSceneFolderDto>(folder)
        };
    }

    public async Task<UpdateKnowledgeSceneFolderResponse> UpdateKnowledgeSceneFolderAsync(UpdateKnowledgeSceneFolderCommand command, CancellationToken cancellationToken)
    {
        if (command.Id <= 0) throw new Exception("FolderId is required.");

        if (string.IsNullOrWhiteSpace(command.Name)) throw new Exception("Folder name is required.");

        var folder = await _knowledgeScenarioDataProvider.GetKnowledgeSceneFolderByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (folder == null) throw new Exception($"Folder [{command.Id}] does not exist.");

        folder.Name = command.Name;
        folder.UpdatedAt = DateTimeOffset.UtcNow;

        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneFolderAsync(folder, cancellationToken: cancellationToken).ConfigureAwait(false);
        Log.Information("UpdateKnowledgeSceneFolderAsync renamed folder. FolderId={@FolderId}, CurrentName={@CurrentName}", folder.Id, folder.Name);

        return new UpdateKnowledgeSceneFolderResponse
        {
            Data = _mapper.Map<KnowledgeSceneFolderDto>(folder)
        };
    }

    public async Task<DeleteKnowledgeSceneFolderResponse> DeleteKnowledgeSceneFolderAsync(DeleteKnowledgeSceneFolderCommand command, CancellationToken cancellationToken)
    {
        if (command.Id <= 0) throw new Exception("FolderId is required.");

        var folder = await _knowledgeScenarioDataProvider.GetKnowledgeSceneFolderByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (folder == null)
            throw new Exception($"DeleteKnowledgeSceneFolderAsync Folder [{command.Id}] does not exist.");

        var scenes = await _knowledgeScenarioDataProvider.GetKnowledgeScenesByFolderIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        var sceneIds = scenes.Select(x => x.Id).ToList();
        var knowledgeSceneItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdsAsync(sceneIds, cancellationToken).ConfigureAwait(false);
        var relations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdsAsync(sceneIds, cancellationToken).ConfigureAwait(false);
        var knowledgeIds = relations.Select(x => x.KnowledgeId).Distinct().ToList();
        
        Log.Information("DeleteKnowledgeSceneFolderAsync loaded related data. FolderId={@FolderId}, SceneIds={@SceneIds}, SceneItemIds={@SceneItemIds}, RelationIds={@RelationIds}, KnowledgeIds={@KnowledgeIds}",
            folder.Id, sceneIds, knowledgeSceneItems.Select(x => x.Id).ToList(), relations.Select(x => x.Id).ToList(), knowledgeIds);

        if (relations.Count != 0)
            await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(relations, false, cancellationToken).ConfigureAwait(false);

        if (knowledgeSceneItems.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneItemsAsync(knowledgeSceneItems, false, cancellationToken).ConfigureAwait(false);

        if (scenes.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeScenesAsync(scenes, false, cancellationToken).ConfigureAwait(false);

        await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneFolderAsync(folder, cancellationToken: cancellationToken).ConfigureAwait(false);

        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);
        Log.Information("DeleteKnowledgeSceneFolderAsync completed. FolderId={@FolderId}", folder.Id);

        return new DeleteKnowledgeSceneFolderResponse
        {
            Data = _mapper.Map<KnowledgeSceneFolderDto>(folder)
        };
    }

    public async Task<AddKnowledgeSceneResponse> AddKnowledgeSceneAsync(AddKnowledgeSceneCommand command, CancellationToken cancellationToken)
    {
        if (command.FolderId <= 0) throw new Exception("AddKnowledgeScene FolderId is required.");

        if (string.IsNullOrWhiteSpace(command.Name)) throw new Exception("AddKnowledgeScene Scene name is required.");

        if (!string.IsNullOrWhiteSpace(command.Description) && command.Description.Trim().Length > 500)
            throw new Exception("AddKnowledgeScene Description max length is 500.");

        var folder = await _knowledgeScenarioDataProvider.GetKnowledgeSceneFolderByIdAsync(command.FolderId, cancellationToken).ConfigureAwait(false);

        if (folder == null)
            throw new Exception($"AddKnowledgeScene Folder [{command.FolderId}] does not exist.");
        
        var duplicatedScene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByFolderAndNameAsync(command.FolderId, command.Name, cancellationToken).ConfigureAwait(false);

        if (duplicatedScene != null)
            throw new Exception($"AddKnowledgeScene Scene [{command.Name}] already exists in this folder.");

        var scene = _mapper.Map<KnowledgeScene>(command);
        scene.Version = string.IsNullOrWhiteSpace(scene.Version) ? "1.0" : scene.Version;
        scene.IsActive = true;

        await _knowledgeScenarioDataProvider.AddKnowledgeSceneAsync(scene, true, cancellationToken: cancellationToken).ConfigureAwait(false);
        var sceneItems = await AddKnowledgeSceneItemsInternalAsync(scene, command.SceneItems, false, cancellationToken).ConfigureAwait(false);
        await SnapshotKnowledgeSceneAsync(scene, sceneItems, scene.Version, true, cancellationToken).ConfigureAwait(false);
        
        Log.Information("AddKnowledgeSceneAsync completed. SceneId={@SceneId}, FolderId={@FolderId}, Status={@Status}, SceneItemCount={@SceneItemCount}", scene.Id, scene.FolderId, scene.Status, sceneItems.Count);

        var sceneDto = _mapper.Map<KnowledgeSceneDto>(scene);
        sceneDto.SceneItems = _mapper.Map<List<KnowledgeSceneItemDto>>(sceneItems);

        return new AddKnowledgeSceneResponse
        {
            Data = sceneDto
        };
    }

    public async Task<UpdateKnowledgeSceneResponse> UpdateKnowledgeSceneAsync(UpdateKnowledgeSceneCommand command, CancellationToken cancellationToken)
    {
        if (command.Id <= 0) throw new Exception("UpdateKnowledgeScene Id is required.");

        if (command.FolderId <= 0) throw new Exception("UpdateKnowledgeScene FolderId is required.");

        if (string.IsNullOrWhiteSpace(command.Name)) throw new Exception("UpdateKnowledgeScene Scene name is required.");

        if (!string.IsNullOrWhiteSpace(command.Description) && command.Description.Trim().Length > 500)
            throw new Exception("UpdateKnowledgeScene Description max length is 500.");

        var folder = await _knowledgeScenarioDataProvider.GetKnowledgeSceneFolderByIdAsync(command.FolderId, cancellationToken).ConfigureAwait(false);

        if (folder == null) throw new Exception($"UpdateKnowledgeScene Folder [{command.FolderId}] does not exist.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (scene == null) throw new Exception($"UpdateKnowledgeScene Scene [{command.Id}] does not exist.");
        
        var duplicatedScene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByFolderAndNameAsync(command.FolderId, command.Name, cancellationToken).ConfigureAwait(false);

        if (duplicatedScene != null && duplicatedScene.Id != command.Id)
            throw new Exception($"UpdateKnowledgeScene Scene [{command.Name}] already exists in this folder.");

        var nextVersion = await GetNextSceneVersionAsync(scene.Id, scene.Version, cancellationToken).ConfigureAwait(false);
        _mapper.Map(command, scene);
        scene.Version = nextVersion;
        scene.IsActive = true;
        var sceneItemsChanged = false;
        if (command.SceneItems != null)
            sceneItemsChanged = await SyncKnowledgeSceneItemsAsync(scene, command.SceneItems, cancellationToken).ConfigureAwait(false);

        scene.UpdatedAt = DateTimeOffset.UtcNow;

        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneAsync(scene, cancellationToken: cancellationToken).ConfigureAwait(false);
        Log.Information("UpdateKnowledgeSceneAsync scene changed. SceneId={@SceneId}, SceneStatus={@SceneStatus}, SceneItemsChanged={@SceneItemsChanged}. Refreshing related prompts.", scene.Id, scene.Status, sceneItemsChanged);
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsBySceneIdsAsync([scene.Id], cancellationToken).ConfigureAwait(false);

        Log.Information("UpdateKnowledgeSceneAsync updated scene. SceneId={@SceneId}, SceneFolderId={@SceneFolderId}, SceneName={@SceneName}", scene.Id, scene.FolderId, scene.Name);

        var sceneItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsAsync(scene.Id, null, cancellationToken).ConfigureAwait(false);
        await SnapshotKnowledgeSceneAsync(scene, sceneItems, scene.Version, true, cancellationToken).ConfigureAwait(false);
        var sceneDto = _mapper.Map<KnowledgeSceneDto>(scene);
        sceneDto.SceneItems = _mapper.Map<List<KnowledgeSceneItemDto>>(sceneItems);

        return new UpdateKnowledgeSceneResponse
        {
            Data = sceneDto
        };
    }

    public async Task<DeleteKnowledgeSceneResponse> DeleteKnowledgeSceneAsync(DeleteKnowledgeSceneCommand command, CancellationToken cancellationToken)
    {
        if (command.Id <= 0) throw new Exception("DeleteKnowledgeScene Id is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (scene == null)
            throw new Exception($"DeleteKnowledgeScene Scene [{command.Id}] does not exist.");

        var sceneItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsAsync(scene.Id, null, cancellationToken).ConfigureAwait(false);
        var (_, histories) = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoriesAsync(scene.Id, null, null, cancellationToken).ConfigureAwait(false);
        var historyItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoryItemsAsync(histories.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);
        var sceneCompanies = await _knowledgeScenarioDataProvider.GetKnowledgeSceneCompaniesAsync([scene.Id], cancellationToken: cancellationToken).ConfigureAwait(false);
        var relations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdAsync(scene.Id, cancellationToken).ConfigureAwait(false);
        var knowledgeIds = relations.Select(x => x.KnowledgeId).Distinct().ToList();

        Log.Information("DeleteKnowledgeSceneAsync loaded related data. SceneId={@SceneId}, SceneItemIds={@SceneItemIds}, HistoryIds={@HistoryIds}, HistoryItemIds={@HistoryItemIds}, CompanyIds={@CompanyIds}, StoreIds={@StoreIds}, RelationIds={@RelationIds}, KnowledgeIds={@KnowledgeIds}",
            scene.Id,
            sceneItems.Select(x => x.Id).ToList(),
            histories.Select(x => x.Id).ToList(),
            historyItems.Select(x => x.Id).ToList(),
            sceneCompanies.Select(x => x.CompanyId).ToList(),
            sceneCompanies.Where(x => x.StoreId.HasValue).Select(x => x.StoreId!.Value).ToList(),
            relations.Select(x => x.Id).ToList(),
            knowledgeIds);

        if (relations.Count != 0)
            await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(relations, false, cancellationToken).ConfigureAwait(false);

        if (sceneCompanies.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneCompaniesAsync(sceneCompanies, false, cancellationToken).ConfigureAwait(false);

        if (historyItems.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneHistoryItemsAsync(historyItems, false, cancellationToken).ConfigureAwait(false);

        if (histories.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneHistoriesAsync(histories, false, cancellationToken).ConfigureAwait(false);

        if (sceneItems.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneItemsAsync(sceneItems, false, cancellationToken).ConfigureAwait(false);

        await _knowledgeScenarioDataProvider.DeleteKnowledgeScenesAsync([scene], true, cancellationToken).ConfigureAwait(false);

        if (knowledgeIds.Count != 0)
            await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);

        Log.Information("DeleteKnowledgeSceneAsync completed. SceneId={@SceneId}", scene.Id);

        var sceneDto = _mapper.Map<KnowledgeSceneDto>(scene);
        sceneDto.SceneItems = _mapper.Map<List<KnowledgeSceneItemDto>>(sceneItems);

        return new DeleteKnowledgeSceneResponse
        {
            Data = sceneDto
        };
    }

    private async Task<List<KnowledgeSceneItem>> AddKnowledgeSceneItemsInternalAsync(KnowledgeScene scene, List<KnowledgeSceneItemDto> items, bool checkExistingItems, CancellationToken cancellationToken)
    {
        if (items == null || items.Count == 0)
            return new List<KnowledgeSceneItem>();

        var itemNames = new List<string>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) throw new Exception("AddKnowledgeSceneItem Name is required.");

            itemNames.Add(item.Name.Trim());
        }

        var duplicatedItemNames = itemNames
            .GroupBy(x => x)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicatedItemNames.Count != 0)
            throw new Exception($"AddKnowledgeSceneItem Items [{string.Join(", ", duplicatedItemNames)}] are duplicated in this request.");

        if (checkExistingItems)
        {
            var duplicatedKnowledges = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneAndNamesAsync(scene.Id, itemNames, cancellationToken).ConfigureAwait(false);

            if (duplicatedKnowledges.Count != 0)
                throw new Exception($"AddKnowledgeSceneItem Items [{string.Join(", ", duplicatedKnowledges.Select(x => x.Name))}] already exist in this scene.");
        }

        var knowledges = items.Select(item =>
        {
            var knowledge = _mapper.Map<KnowledgeSceneItem>(item);
            knowledge.SceneId = scene.Id;
            knowledge.UpdatedAt ??= knowledge.CreatedAt;
            return knowledge;
        }).ToList();

        await _knowledgeScenarioDataProvider.AddKnowledgeSceneItemsAsync(knowledges, false, cancellationToken).ConfigureAwait(false);

        scene.UpdatedAt = DateTimeOffset.UtcNow;
        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneAsync(scene, true, cancellationToken).ConfigureAwait(false);

        return knowledges;
    }

    private async Task<bool> SyncKnowledgeSceneItemsAsync(KnowledgeScene scene, List<KnowledgeSceneItemDto> items, CancellationToken cancellationToken)
    {
        var targetItems = items ?? new List<KnowledgeSceneItemDto>();
        var existingItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsAsync(scene.Id, null, cancellationToken).ConfigureAwait(false);
        var existingById = existingItems.ToDictionary(x => x.Id);

        var itemNames = new List<string>();
        foreach (var item in targetItems)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) throw new Exception("UpdateKnowledgeSceneItem Name is required.");
            itemNames.Add(item.Name.Trim());
        }

        var duplicatedItemNames = itemNames
            .GroupBy(x => x)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicatedItemNames.Count != 0)
            throw new Exception($"UpdateKnowledgeSceneItem Items [{string.Join(", ", duplicatedItemNames)}] are duplicated in this request.");

        var requestIds = targetItems.Where(x => x.Id > 0).Select(x => x.Id).Distinct().ToList();
        var invalidIds = requestIds.Where(x => !existingById.ContainsKey(x)).ToList();
        if (invalidIds.Count != 0)
            throw new Exception($"UpdateKnowledgeSceneItem Items [{string.Join(", ", invalidIds)}] do not exist in this scene.");

        var requestIdSet = requestIds.ToHashSet();
        var itemsToDelete = existingItems.Where(x => !requestIdSet.Contains(x.Id)).ToList();
        var itemsToAdd = new List<KnowledgeSceneItem>();
        var hasChanges = itemsToDelete.Count != 0;

        foreach (var item in targetItems)
        {
            if (item.Id > 0)
            {
                var existingItem = existingById[item.Id];
                var updatedName = item.Name.Trim();
                var updatedContent = item.Content?.Trim();
                var updatedFileName = item.FileName?.Trim();
                var changed = existingItem.Name != updatedName
                    || existingItem.Type != item.Type
                    || existingItem.Content != updatedContent
                    || existingItem.FileName != updatedFileName;

                if (!changed)
                    continue;

                existingItem.Name = updatedName;
                existingItem.Type = item.Type;
                existingItem.Content = updatedContent;
                existingItem.FileName = updatedFileName;
                existingItem.UpdatedAt = DateTimeOffset.UtcNow;
                await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneItemAsync(existingItem, false, cancellationToken).ConfigureAwait(false);
                hasChanges = true;
            }
            else
            {
                var knowledge = _mapper.Map<KnowledgeSceneItem>(item);
                knowledge.SceneId = scene.Id;
                knowledge.UpdatedAt ??= knowledge.CreatedAt;
                itemsToAdd.Add(knowledge);
            }
        }

        if (itemsToAdd.Count != 0)
        {
            await _knowledgeScenarioDataProvider.AddKnowledgeSceneItemsAsync(itemsToAdd, false, cancellationToken).ConfigureAwait(false);
            hasChanges = true;
        }

        if (itemsToDelete.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneItemsAsync(itemsToDelete, false, cancellationToken).ConfigureAwait(false);

        return hasChanges;
    }

    public async Task<GetKnowledgeSceneFoldersResponse> GetKnowledgeSceneFoldersAsync(GetKnowledgeSceneFoldersRequest request, CancellationToken cancellationToken)
    {
        var folders = await _knowledgeScenarioDataProvider.GetKnowledgeSceneFoldersAsync(request.Keyword, cancellationToken).ConfigureAwait(false);
        Log.Information("GetKnowledgeSceneFoldersAsync completed. FolderCount={Count}", folders.Count);

        return new GetKnowledgeSceneFoldersResponse
        {
            Data = _mapper.Map<List<KnowledgeSceneFolderDto>>(folders)
        };
    }

    public async Task<GetKnowledgeScenesResponse> GetKnowledgeScenesAsync(GetKnowledgeScenesRequest request, CancellationToken cancellationToken)
    {
        if (request.FolderId <= 0) throw new Exception("GetKnowledgeScenes FolderId is required.");

        var scenes = await _knowledgeScenarioDataProvider.GetKnowledgeScenesAsync(request.FolderId, request.Keyword, cancellationToken).ConfigureAwait(false);
        var sceneItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneIdsAsync(scenes.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);
        var sceneItemMap = sceneItems.GroupBy(x => x.SceneId).ToDictionary(x => x.Key, x => x.ToList());
        
        Log.Information("GetKnowledgeScenesAsync completed. FolderId={FolderId}, SceneCount={Count}", request.FolderId, scenes.Count);

        var sceneDtos = scenes.Select(scene =>
        {
            var sceneDto = _mapper.Map<KnowledgeSceneDto>(scene);
            sceneDto.SceneItems = _mapper.Map<List<KnowledgeSceneItemDto>>(sceneItemMap.TryGetValue(scene.Id, out var items) ? items : []);
            sceneDto.SceneItems.ForEach(x => x.SceneStatus = scene.Status);
            return sceneDto;
        }).ToList();

        return new GetKnowledgeScenesResponse
        {
            Data = sceneDtos
        };
    }

    public async Task<GetKnowledgeSceneResponse> GetKnowledgeSceneAsync(GetKnowledgeSceneRequest request, CancellationToken cancellationToken)
    {
        if (request.Id <= 0) throw new Exception("GetKnowledgeScene Id is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"GetKnowledgeScene Scene [{request.Id}] does not exist.");

        var knowledges = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsAsync(request.Id, null, cancellationToken).ConfigureAwait(false);
        var sceneDto = _mapper.Map<KnowledgeSceneDto>(scene);
        sceneDto.SceneItems = _mapper.Map<List<KnowledgeSceneItemDto>>(knowledges);
        sceneDto.SceneItems.ForEach(x => x.SceneStatus = scene.Status);
        Log.Information("GetKnowledgeSceneAsync completed. SceneId={SceneId}, SceneItemCount={SceneItemCount}", scene.Id, knowledges.Count);
      
        return new GetKnowledgeSceneResponse
        {
            Data = new GetKnowledgeSceneResponseData
            {
                SceneData = sceneDto
            }
        };
    }

    public async Task<GetKnowledgeSceneItemsResponse> GetKnowledgeSceneItemsAsync(GetKnowledgeSceneItemsRequest request, CancellationToken cancellationToken)
    {
        if (request.SceneId <= 0) throw new Exception("GetKnowledgeSceneItems SceneId is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(request.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"GetKnowledgeSceneItems Scene [{request.SceneId}] does not exist.");

        var knowledges = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsAsync(request.SceneId, request.Keyword, cancellationToken).ConfigureAwait(false);
        Log.Information("GetKnowledgeSceneItemsAsync completed. SceneId={SceneId}, SceneItemCount={Count}", request.SceneId, knowledges.Count);

        return new GetKnowledgeSceneItemsResponse
        {
            Data = _mapper.Map<List<KnowledgeSceneItemDto>>(knowledges).Select(x =>
            {
                x.SceneStatus = scene.Status;
                return x;
            }).ToList()
        };
    }

    public async Task<GetKnowledgeSceneHistoryResponse> GetKnowledgeSceneHistoryAsync(GetKnowledgeSceneHistoryRequest request, CancellationToken cancellationToken)
    {
        if (request.SceneId <= 0) throw new Exception("GetKnowledgeSceneHistory SceneId is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(request.SceneId, cancellationToken).ConfigureAwait(false);
        if (scene == null)
            throw new Exception($"GetKnowledgeSceneHistory Scene [{request.SceneId}] does not exist.");

        var (count, histories) = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoriesAsync(request.SceneId, request.PageIndex, request.PageSize, cancellationToken).ConfigureAwait(false);
        var historyItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoryItemsAsync(histories.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);
        var itemMap = historyItems.GroupBy(x => x.HistoryId).ToDictionary(x => x.Key, x => x.ToList());

        var sceneDtos = histories.Select(history =>
        {
            var dto = _mapper.Map<KnowledgeSceneHistoryDto>(history);
            dto.SceneItems = MapHistoryItems(itemMap.TryGetValue(history.Id, out var items) ? items : [], history.SceneId, history.Status);
            return dto;
        }).ToList();

        return new GetKnowledgeSceneHistoryResponse
        {
            Data = new GetKnowledgeSceneHistoryResponseData
            {
                Count = count,
                Scenes = sceneDtos
            }
        };
    }

    public async Task<GetKnowledgeSceneMarketResponse> GetKnowledgeSceneMarketAsync(GetKnowledgeSceneMarketRequest request, CancellationToken cancellationToken)
    {
        if (request.CompanyId <= 0) throw new Exception("GetKnowledgeSceneMarket CompanyId is required.");
        if (request.StoreId <= 0) throw new Exception("GetKnowledgeSceneMarket StoreId is required.");

        await EnsureStoreInCompanyAsync(request.CompanyId, request.StoreId, cancellationToken).ConfigureAwait(false);

        var (sceneCompanies, sceneStoreApplications) =
            await LoadMarketSceneRowsAsync(request.CompanyId, request.StoreId, cancellationToken).ConfigureAwait(false);

        if (sceneCompanies.Count == 0)
            return EmptyMarketResponse();

        var scenes = await LoadAuthorizedScenesAsync(sceneCompanies, cancellationToken).ConfigureAwait(false);
        if (scenes.Count == 0)
            return EmptyMarketResponse();

        var keyword = request.Keyword?.Trim();
        var visibleScenes = FilterMarketScenes(scenes, sceneStoreApplications, keyword, request.MarketType);

        return BuildMarketResponse(visibleScenes, sceneStoreApplications);
    }

    private static GetKnowledgeSceneMarketResponse EmptyMarketResponse()
    {
        return new GetKnowledgeSceneMarketResponse
        {
            Data = new GetKnowledgeSceneMarketResponseData
            {
                Scenes = new List<KnowledgeSceneDto>()
            }
        };
    }

    private async Task EnsureStoreInCompanyAsync(int companyId, int storeId, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: storeId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (store == null || store.CompanyId != companyId)
            throw new Exception($"Store [{storeId}] does not belong to Company [{companyId}].");
    }

    private async Task<(List<KnowledgeSceneCompany> SceneCompanies, List<KnowledgeSceneCompany> SceneStoreApplications)>
        LoadMarketSceneRowsAsync(int companyId, int storeId, CancellationToken cancellationToken)
    {
        var rows = await _knowledgeScenarioDataProvider
            .GetKnowledgeSceneCompanyStoreApplicationsAsync(companyId, storeId, cancellationToken)
            .ConfigureAwait(false);

        var sceneCompanies = rows.Where(x => x.StoreId == null).ToList();
        var sceneStoreApplications = rows.Where(x => x.StoreId == storeId).ToList();

        Log.Information("GetKnowledgeSceneMarket rows loaded. Total={Total}, CompanyAuthCount={CompanyAuthCount}, StoreApplyCount={StoreApplyCount}",
            rows.Count, sceneCompanies.Count, sceneStoreApplications.Count);

        return (sceneCompanies, sceneStoreApplications);
    }

    private async Task<List<KnowledgeScene>> LoadAuthorizedScenesAsync(List<KnowledgeSceneCompany> sceneCompanies, CancellationToken cancellationToken)
    {
        var sceneIds = sceneCompanies.Select(x => x.SceneId).Distinct().ToList();
        Log.Information("GetKnowledgeSceneMarket authorized scene ids {@SceneIds}", sceneIds);
        return await _knowledgeScenarioDataProvider.GetKnowledgeScenesByIdsAsync(sceneIds, cancellationToken).ConfigureAwait(false);
    }

    private static List<KnowledgeScene> FilterMarketScenes(List<KnowledgeScene> scenes, List<KnowledgeSceneCompany> storeApplications, string keyword, KnowledgeSceneMarketType marketType)
    {
        var list = scenes
            .Where(x => string.IsNullOrWhiteSpace(keyword) || x.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.IsActive)
            .ToList();
        
        if (marketType == KnowledgeSceneMarketType.MyTemplates)
        {
            return list
                .Where(x => IsApplied(storeApplications, x.Id))
                .ToList();
        }
        
        return list
            .Where(x => x.Status == KnowledgeSceneStatus.Published || IsApplied(storeApplications, x.Id))
            .ToList();
    }

    private static bool IsApplied(List<KnowledgeSceneCompany> storeApplications, int sceneId)
        => storeApplications.Any(x => x.SceneId == sceneId && x.IsApplied);

    private GetKnowledgeSceneMarketResponse BuildMarketResponse(List<KnowledgeScene> scenes, List<KnowledgeSceneCompany> storeApplications)
    {
        return new GetKnowledgeSceneMarketResponse
        {
            Data = new GetKnowledgeSceneMarketResponseData
            {
                Scenes = scenes.Select(scene =>
                {
                    var dto = _mapper.Map<KnowledgeSceneDto>(scene);
                    dto.IsApplied = IsApplied(storeApplications, scene.Id);
                    return dto;
                }).ToList()
            }
        };
    }

    public async Task<GetKnowledgeSceneCompaniesResponse> GetKnowledgeSceneCompaniesAsync(GetKnowledgeSceneCompaniesRequest request, CancellationToken cancellationToken)
    {
        if (request.SceneId <= 0) throw new Exception("GetKnowledgeSceneCompanies SceneId is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(request.SceneId, cancellationToken).ConfigureAwait(false);
        if (scene == null)
            throw new Exception($"GetKnowledgeSceneCompanies Scene [{request.SceneId}] does not exist.");

        var sceneCompanies = await _knowledgeScenarioDataProvider.GetKnowledgeSceneCompaniesAsync(sceneIds: [request.SceneId], isCompanyAuthorization: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GetKnowledgeSceneCompaniesResponse
        {
            Data = _mapper.Map<List<KnowledgeSceneCompanyDto>>(sceneCompanies.OrderBy(x => x.CompanyId).ToList())
        };
    }

    public async Task<GetKnowledgeSceneRelatedKnowledgesResponse> GetKnowledgeSceneRelatedKnowledgesAsync(GetKnowledgeSceneRelatedKnowledgesRequest request, CancellationToken cancellationToken)
    {
        if (request.SceneId <= 0) throw new Exception("GetKnowledgeSceneRelatedKnowledges SceneId is required.");
        if (request.StoreId <= 0) throw new Exception("GetKnowledgeSceneRelatedKnowledges StoreId is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(request.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"GetKnowledgeSceneRelatedKnowledges Scene [{request.SceneId}] does not exist.");

        var relations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdAsync(request.SceneId, cancellationToken).ConfigureAwait(false);
        var data = await BuildRelatedAssistantIdsByStoreAsync(relations, request.StoreId, cancellationToken).ConfigureAwait(false);
        Log.Information("GetKnowledgeSceneRelatedKnowledgesAsync completed. SceneId={SceneId}, RelationCount={RelationCount}, ActiveKnowledgeCount={KnowledgeCount}",
            request.SceneId, relations.Count, data.Count);
  
        return new GetKnowledgeSceneRelatedKnowledgesResponse
        {
            Data = data
        };
    }

    public async Task<SaveKnowledgeSceneRelatedKnowledgesResponse> SaveKnowledgeSceneRelatedKnowledgesAsync(SaveKnowledgeSceneRelatedKnowledgesCommand command, CancellationToken cancellationToken)
    {
        if (command.SceneId <= 0) throw new Exception("SaveKnowledgeSceneRelatedKnowledges SceneId is required.");
        if (command.StoreId <= 0) throw new Exception("SaveKnowledgeSceneRelatedKnowledges StoreId is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(command.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"SaveKnowledgeSceneRelatedKnowledges Scene [{command.SceneId}] does not exist.");
        
        var targetAssistantIds = (command.AssistantIds ?? []).Where(x => x > 0).Distinct().ToList();
        
        var currentStoreRelations = await _aiSpeechAssistantDataProvider.GetStoreKnowledgeSceneRelationsBySceneIdAsync(command.StoreId, command.SceneId, cancellationToken).ConfigureAwait(false);
        
        var targetKnowledgeIds = await _aiSpeechAssistantDataProvider.GetStoreActiveKnowledgeIdsByAssistantIdsAsync(command.StoreId, targetAssistantIds, cancellationToken).ConfigureAwait(false);

        var currentKnowledgeIdSet = currentStoreRelations.Select(x => x.KnowledgeId).ToHashSet();
        var targetKnowledgeIdSet = targetKnowledgeIds.ToHashSet();

        var relationsToDelete = currentStoreRelations.Where(x => !targetKnowledgeIdSet.Contains(x.KnowledgeId)).ToList();
        var knowledgeIdsToAdd = targetKnowledgeIds.Where(x => !currentKnowledgeIdSet.Contains(x)).ToList();
        
        Log.Information("SaveKnowledgeSceneRelatedKnowledgesAsync diff built. SceneId={SceneId}, StoreId={StoreId}, CurrentStoreCount={CurrentCount}, TargetCount={TargetCount}, DeleteCount={DeleteCount}, AddCount={AddCount}",
            command.SceneId, command.StoreId, currentKnowledgeIdSet.Count, targetKnowledgeIds.Count, relationsToDelete.Count, knowledgeIdsToAdd.Count);
        if (relationsToDelete.Count != 0)
            await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(relationsToDelete, true, cancellationToken).ConfigureAwait(false);

        if (knowledgeIdsToAdd.Count != 0)
        {
            var relationsToAdd = knowledgeIdsToAdd.Select(knowledgeId => new AiSpeechAssistantKnowledgeSceneRelation
            {
                KnowledgeId = knowledgeId,
                SceneId = command.SceneId
            }).ToList();

            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgeSceneRelationsAsync(relationsToAdd, true, cancellationToken).ConfigureAwait(false);
        }

        var affectedKnowledgeIds = currentKnowledgeIdSet.Union(targetKnowledgeIdSet).ToList();

        if (affectedKnowledgeIds.Count != 0)
        {
            Log.Information("SaveKnowledgeSceneRelatedKnowledgesAsync refresh prompts. SceneId={SceneId}, AffectedKnowledgeCount={AffectedKnowledgeCount}", command.SceneId, affectedKnowledgeIds.Count);
            await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsAsync(affectedKnowledgeIds, cancellationToken).ConfigureAwait(false);
        }

        var latestStoreRelations = await _aiSpeechAssistantDataProvider.GetStoreKnowledgeSceneRelationsBySceneIdAsync(command.StoreId, command.SceneId, cancellationToken).ConfigureAwait(false);
        var latestStoreKnowledgeIds = latestStoreRelations.OrderByDescending(x => x.CreatedAt).Select(x => x.KnowledgeId).ToList();
        Log.Information("SaveKnowledgeSceneRelatedKnowledgesAsync completed. SceneId={SceneId}, StoreId={StoreId}, SavedKnowledgeCount={KnowledgeCount}", command.SceneId, command.StoreId, latestStoreKnowledgeIds.Count);

        return new SaveKnowledgeSceneRelatedKnowledgesResponse
        {
            Data = latestStoreKnowledgeIds
        };
    }

    public async Task<SaveKnowledgeSceneCompaniesResponse> SaveKnowledgeSceneCompaniesAsync(SaveKnowledgeSceneCompaniesCommand command, CancellationToken cancellationToken)
    {
        if (command.SceneId <= 0) throw new Exception("SaveKnowledgeSceneCompanies SceneId is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(command.SceneId, cancellationToken).ConfigureAwait(false);
        if (scene == null)
            throw new Exception($"SaveKnowledgeSceneCompanies Scene [{command.SceneId}] does not exist.");

        var targetCompanyIds = (command.CompanyIds ?? []).Where(x => x > 0).Distinct().ToList();

        var currentCompanies = await _knowledgeScenarioDataProvider.GetKnowledgeSceneCompaniesAsync(sceneIds: [command.SceneId], isCompanyAuthorization: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        var currentCompanyIdSet = currentCompanies.Select(x => x.CompanyId).ToHashSet();
        var targetCompanyIdSet = targetCompanyIds.ToHashSet();

        var companiesToDelete = currentCompanies.Where(x => !targetCompanyIdSet.Contains(x.CompanyId)).ToList();
        var companiesToAdd = targetCompanyIds
            .Where(x => !currentCompanyIdSet.Contains(x))
            .Select(x => new KnowledgeSceneCompany
            {
                SceneId = command.SceneId,
                CompanyId = x,
                StoreId = null,
                IsApplied = false,
                AppliedAt = null,
                AuthorizedAt = DateTimeOffset.UtcNow
            }).ToList();

        var storeApplicationsToDelete = companiesToDelete.Count == 0
            ? []
            : await _knowledgeScenarioDataProvider.GetKnowledgeSceneCompaniesAsync(sceneIds: [command.SceneId], isCompanyAuthorization: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        storeApplicationsToDelete = storeApplicationsToDelete
            .Where(x => companiesToDelete.Any(c => c.CompanyId == x.CompanyId))
            .ToList();

        if (storeApplicationsToDelete.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneCompaniesAsync(storeApplicationsToDelete, false, cancellationToken).ConfigureAwait(false);

        if (companiesToDelete.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneCompaniesAsync(companiesToDelete, false, cancellationToken).ConfigureAwait(false);

        if (companiesToAdd.Count != 0)
            await _knowledgeScenarioDataProvider.AddKnowledgeSceneCompaniesAsync(companiesToAdd, true, cancellationToken).ConfigureAwait(false);

        var latestCompanies = await _knowledgeScenarioDataProvider.GetKnowledgeSceneCompaniesAsync(sceneIds: [command.SceneId], isCompanyAuthorization: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new SaveKnowledgeSceneCompaniesResponse
        {
            Data = _mapper.Map<List<KnowledgeSceneCompanyDto>>(latestCompanies.OrderBy(x => x.CompanyId).ToList())
        };
    }

    public async Task<UpdateKnowledgeSceneCompanyResponse> UpdateKnowledgeSceneCompanyAsync(UpdateKnowledgeSceneCompanyCommand command, CancellationToken cancellationToken)
    {
        if (command.SceneId <= 0) throw new Exception("UpdateKnowledgeSceneCompany SceneId is required.");
        if (command.CompanyId <= 0) throw new Exception("UpdateKnowledgeSceneCompany CompanyId is required.");
        if (command.StoreId <= 0) throw new Exception("UpdateKnowledgeSceneCompany StoreId is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(command.SceneId, cancellationToken).ConfigureAwait(false);
        if (scene == null)
            throw new Exception($"UpdateKnowledgeSceneCompany Scene [{command.SceneId}] does not exist.");

        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: command.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (store == null || store.CompanyId != command.CompanyId)
            throw new Exception($"Store [{command.StoreId}] does not belong to Company [{command.CompanyId}].");
        
        var sceneCompany = await _knowledgeScenarioDataProvider.GetKnowledgeSceneCompanyAsync(command.SceneId, command.CompanyId, cancellationToken).ConfigureAwait(false);
        if (sceneCompany == null)
            throw new Exception($"UpdateKnowledgeSceneCompany Scene [{command.SceneId}] is not authorized for Company [{command.CompanyId}].");

        var sceneStore = await _knowledgeScenarioDataProvider.GetKnowledgeSceneCompanyStoreAsync(command.SceneId, command.StoreId, cancellationToken).ConfigureAwait(false);
        if (sceneStore == null)
        {
            sceneStore = new KnowledgeSceneCompany
            {
                SceneId = command.SceneId,
                CompanyId = command.CompanyId,
                StoreId = command.StoreId,
                AuthorizedAt = sceneCompany.AuthorizedAt
            };
        }

        sceneStore.CompanyId = command.CompanyId;
        sceneStore.IsApplied = command.IsApplied;
        sceneStore.AppliedAt = command.IsApplied ? DateTimeOffset.UtcNow : null;

        if (sceneStore.Id <= 0)
            await _knowledgeScenarioDataProvider.AddKnowledgeSceneCompaniesAsync([sceneStore], true, cancellationToken).ConfigureAwait(false);
        else
            await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneCompanyAsync(sceneStore, true, cancellationToken).ConfigureAwait(false);

        if (!command.IsApplied)
            await RemoveSceneRelationsFromStoreKnowledgeAsync(command.SceneId, command.StoreId, cancellationToken).ConfigureAwait(false);

        return new UpdateKnowledgeSceneCompanyResponse
        {
            Data = _mapper.Map<KnowledgeSceneCompanyDto>(sceneStore)
        };
    }

    private async Task RemoveSceneRelationsFromStoreKnowledgeAsync(int sceneId, int storeId, CancellationToken cancellationToken)
    { 
        var assistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantsByStoreIdAsync(storeId, cancellationToken).ConfigureAwait(false);
        var assistantIds = assistants.Select(x => x.Id).Distinct().ToList();
        if (assistantIds.Count == 0)
            return;

        var knowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantActiveKnowledgesAsync(assistantIds, cancellationToken).ConfigureAwait(false);
        var knowledgeIds = knowledges.Select(x => x.Id).Distinct().ToList();
        if (knowledgeIds.Count == 0)
            return;

        var relations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdAsync(sceneId, cancellationToken).ConfigureAwait(false);
        var toDelete = relations.Where(x => knowledgeIds.Contains(x.KnowledgeId)).ToList();
        if (toDelete.Count == 0)
            return;

        await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(toDelete, true, cancellationToken).ConfigureAwait(false);
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UpdateKnowledgeSceneHistoryResponse> UpdateKnowledgeSceneHistoryAsync(UpdateKnowledgeSceneHistoryCommand command, CancellationToken cancellationToken)
    {
        if (command.SceneId <= 0) throw new Exception("UpdateKnowledgeSceneHistory SceneId is required.");
        if (command.HistoryId <= 0) throw new Exception("UpdateKnowledgeSceneHistory HistoryId is required.");

        var history = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoryByIdAsync(command.HistoryId, cancellationToken).ConfigureAwait(false);
        if (history == null || history.SceneId != command.SceneId)
            throw new Exception($"UpdateKnowledgeSceneHistory History [{command.HistoryId}] does not exist in this scene.");

        history.Brief = string.IsNullOrWhiteSpace(command.Brief) ? "未命名改動" : command.Brief.Trim();
        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneHistoriesAsync([history], true, cancellationToken).ConfigureAwait(false);

        var historyItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoryItemsAsync([history.Id], cancellationToken).ConfigureAwait(false);
        var dto = _mapper.Map<KnowledgeSceneHistoryDto>(history);
        dto.SceneItems = MapHistoryItems(historyItems, history.SceneId, history.Status);

        return new UpdateKnowledgeSceneHistoryResponse
        {
            Data = dto
        };
    }

    public async Task<SwitchKnowledgeSceneVersionResponse> SwitchKnowledgeSceneVersionAsync(SwitchKnowledgeSceneVersionCommand command, CancellationToken cancellationToken)
    {
        if (command.SceneId <= 0) throw new Exception("SwitchKnowledgeSceneVersion SceneId is required.");
        if (command.HistoryId <= 0) throw new Exception("SwitchKnowledgeSceneVersion HistoryId is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(command.SceneId, cancellationToken).ConfigureAwait(false);
        if (scene == null)
            throw new Exception($"SwitchKnowledgeSceneVersion Scene [{command.SceneId}] does not exist.");

        var targetHistory = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoryByIdAsync(command.HistoryId, cancellationToken).ConfigureAwait(false);
        if (targetHistory == null || targetHistory.SceneId != command.SceneId)
            throw new Exception($"SwitchKnowledgeSceneVersion History [{command.HistoryId}] does not exist in this scene.");

        var historyItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoryItemsAsync([targetHistory.Id], cancellationToken).ConfigureAwait(false);
        var currentItems = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsAsync(scene.Id, null, cancellationToken).ConfigureAwait(false);

        if (currentItems.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneItemsAsync(currentItems, false, cancellationToken).ConfigureAwait(false);

        var restoredItems = historyItems.Select(item => new KnowledgeSceneItem
        {
            SceneId = scene.Id,
            Name = item.Name,
            Type = item.Type,
            Content = item.Content,
            FileName = item.FileName,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        }).ToList();

        if (restoredItems.Count != 0)
            await _knowledgeScenarioDataProvider.AddKnowledgeSceneItemsAsync(restoredItems, false, cancellationToken).ConfigureAwait(false);

        scene.FolderId = targetHistory.FolderId;
        scene.Name = targetHistory.Name;
        scene.Description = targetHistory.Description;
        scene.Version = targetHistory.Version;
        scene.Status = targetHistory.Status;
        scene.IsActive = true;
        scene.UpdatedAt = DateTimeOffset.UtcNow;

        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneAsync(scene, true, cancellationToken).ConfigureAwait(false);

        var (_, allHistories) = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoriesAsync(scene.Id, null, null, cancellationToken).ConfigureAwait(false);
        foreach (var history in allHistories)
            history.IsActive = history.Id == targetHistory.Id;
        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneHistoriesAsync(allHistories, true, cancellationToken).ConfigureAwait(false);

        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsBySceneIdsAsync([scene.Id], cancellationToken).ConfigureAwait(false);

        var sceneDto = _mapper.Map<KnowledgeSceneDto>(scene);
        sceneDto.SceneItems = _mapper.Map<List<KnowledgeSceneItemDto>>(restoredItems);

        return new SwitchKnowledgeSceneVersionResponse
        {
            Data = sceneDto
        };
    }

    private async Task<List<int>> BuildRelatedAssistantIdsByStoreAsync(List<AiSpeechAssistantKnowledgeSceneRelation> relations, int storeId, CancellationToken cancellationToken)
    {
        if (relations == null || relations.Count == 0)
            return new List<int>();

        var storeAssistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantsByStoreIdAsync(storeId, cancellationToken).ConfigureAwait(false);
        var storeAssistantIdSet = storeAssistants.Select(x => x.Id).ToHashSet();
        if (storeAssistantIdSet.Count == 0)
            return new List<int>();

        var knowledgeIds = relations.Select(x => x.KnowledgeId).Distinct().ToList();
        var knowledgeList = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);

        return knowledgeList.Where(k => storeAssistantIdSet.Contains(k.AssistantId)).Select(k => k.AssistantId).Distinct().ToList();
    }

    private async Task<string> GetNextSceneVersionAsync(int sceneId, string currentVersion, CancellationToken cancellationToken)
    {
        var (_, histories) = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoriesAsync(sceneId, null, null, cancellationToken).ConfigureAwait(false);
        var versions = histories.Select(x => x.Version).ToList();
        versions.Add(currentVersion);

        var maxMajor = 1;
        var maxMinor = 0;

        foreach (var version in versions.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var parts = version.Split('.');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
                continue;

            if (major > maxMajor || (major == maxMajor && minor > maxMinor))
            {
                maxMajor = major;
                maxMinor = minor;
            }
        }

        return $"{maxMajor}.{maxMinor + 1}";
    }

    private static List<KnowledgeSceneItemDto> MapHistoryItems(List<KnowledgeSceneHistoryItem> historyItems, int sceneId, KnowledgeSceneStatus sceneStatus)
    {
        return historyItems.Select(item => new KnowledgeSceneItemDto
        {
            Id = item.SceneItemId ?? 0,
            SceneId = sceneId,
            Name = item.Name,
            Type = item.Type,
            Content = item.Content,
            FileName = item.FileName,
            SceneStatus = sceneStatus,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        }).ToList();
    }
    
    private async Task SnapshotKnowledgeSceneAsync(KnowledgeScene scene, List<KnowledgeSceneItem> sceneItems, string version, bool isActive, CancellationToken cancellationToken)
    {
        var (_, histories) = await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoriesAsync(scene.Id, null, null, cancellationToken).ConfigureAwait(false);
        var previousHistory = histories.FirstOrDefault();
        var previousHistoryItems = previousHistory == null
            ? new List<KnowledgeSceneHistoryItem>()
            : await _knowledgeScenarioDataProvider.GetKnowledgeSceneHistoryItemsAsync([previousHistory.Id], cancellationToken).ConfigureAwait(false);

        foreach (var history in histories)
            history.IsActive = false;
        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneHistoriesAsync(histories, false, cancellationToken).ConfigureAwait(false);

        var brief = await GenerateSceneChangeBriefAsync(scene, sceneItems, previousHistory, previousHistoryItems, cancellationToken).ConfigureAwait(false);

        var historyEntity = new KnowledgeSceneHistory
        {
            SceneId = scene.Id,
            FolderId = scene.FolderId,
            Name = scene.Name,
            Description = scene.Description,
            Version = version,
            Brief = brief,
            Status = scene.Status,
            IsActive = isActive,
            CreatedAt = scene.CreatedAt,
            UpdatedAt = scene.UpdatedAt,
            SnapshotAt = DateTimeOffset.UtcNow
        };

        await _knowledgeScenarioDataProvider.AddKnowledgeSceneHistoryAsync(historyEntity, true, cancellationToken).ConfigureAwait(false);

        var historyItems = sceneItems.Select(item => new KnowledgeSceneHistoryItem
        {
            HistoryId = historyEntity.Id,
            SceneItemId = item.Id,
            Name = item.Name,
            Type = item.Type,
            Content = item.Content,
            FileName = item.FileName,
            CreatedAt = item.CreatedAt == default ? scene.CreatedAt : item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        }).ToList();

        await _knowledgeScenarioDataProvider.AddKnowledgeSceneHistoryItemsAsync(historyItems, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GenerateSceneChangeBriefAsync(KnowledgeScene scene, List<KnowledgeSceneItem> sceneItems, KnowledgeSceneHistory previousHistory, List<KnowledgeSceneHistoryItem> previousHistoryItems, CancellationToken cancellationToken)
    {
        try
        {
            var oldComparableJson = BuildSceneComparableJson(previousHistory, previousHistoryItems);
            var newComparableJson = BuildSceneComparableJson(scene, sceneItems);
            var diff = CompareJsons(oldComparableJson, newComparableJson);

            if (diff == null || !diff.HasValues)
                return  "未命名改動";

            Log.Information("GenerateSceneChangeBriefAsync diff generated. SceneId={SceneId}, Diff={Diff}", scene.Id, diff);

            var brief = await GenerateKnowledgeChangeBriefAsync(diff.ToString(Formatting.None), cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(brief) ?  "未命名改動" : brief.Trim();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Generate scene history brief error. SceneId={SceneId}", scene.Id);
            return  "未命名改動";
        }
    }

    private static string BuildSceneComparableJson(KnowledgeSceneHistory history, IEnumerable<KnowledgeSceneHistoryItem> items)
    {
        if (history == null)
            return "{}";

        return BuildSceneComparableJsonCore(
            history.Name,
            history.Description,
            history.Status,
            items?.Select(x => new SceneComparableItem(x.Name, x.Type, x.Content, x.FileName)));
    }

    private static string BuildSceneComparableJson(KnowledgeScene scene, IEnumerable<KnowledgeSceneItem> items)
    {
        if (scene == null)
            return "{}";

        return BuildSceneComparableJsonCore(
            scene.Name,
            scene.Description,
            scene.Status,
            items?.Select(x => new SceneComparableItem(x.Name, x.Type, x.Content, x.FileName)));
    }

    private static string BuildSceneComparableJsonCore(string name, string description, KnowledgeSceneStatus status, IEnumerable<SceneComparableItem> items)
    {
        var obj = new JObject
        {
            ["name"] = name ?? string.Empty,
            ["description"] = description ?? string.Empty,
            ["status"] = (int)status
        };

        var itemArray = new JArray(
            (items ?? Enumerable.Empty<SceneComparableItem>())
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => (int)x.Type)
            .ThenBy(x => x.FileName, StringComparer.Ordinal)
            .ThenBy(x => x.Content, StringComparer.Ordinal)
            .Select(x => new JObject
            {
                ["name"] = x.Name ?? string.Empty,
                ["type"] = (int)x.Type,
                ["content"] = x.Content ?? string.Empty,
                ["fileName"] = x.FileName ?? string.Empty
            }));

        obj["sceneItems"] = itemArray;
        return obj.ToString(Formatting.None);
    }

    private JObject CompareJsons(string oldJson, string newJson)
    {
        var result = new JObject();
        var oldObj = JObject.Parse(oldJson);
        var newObj = JObject.Parse(newJson);

        foreach (var property in oldObj.Properties())
        {
            var key = property.Name;
            var oldValue = property.Value;
            var newValue = newObj.TryGetValue(key, out var value) ? value : null;

            if (!JToken.DeepEquals(oldValue, newValue))
            {
                if (oldValue is JArray oldArray && newValue is JArray newArray)
                {
                    var arrayDiff = CompareJArrays(oldArray, newArray);
                    if (arrayDiff.Count > 0)
                        result[key] = arrayDiff;
                }
                else
                {
                    result[key] = new JArray
                    {
                        new JObject
                        {
                            ["old"] = oldValue,
                            ["new"] = newValue
                        }
                    };
                }
            }
        }

        foreach (var property in newObj.Properties())
        {
            var key = property.Name;
            if (!oldObj.ContainsKey(key))
            {
                result[key] = new JArray
                {
                    new JObject
                    {
                        ["old"] = null,
                        ["new"] = property.Value
                    }
                };
            }
        }

        return result;
    }

    private static JArray CompareJArrays(JArray oldArray, JArray newArray)
    {
        var diff = new JArray();
        var maxLength = Math.Max(oldArray.Count, newArray.Count);

        for (var i = 0; i < maxLength; i++)
        {
            var oldValue = i < oldArray.Count ? oldArray[i] : null;
            var newValue = i < newArray.Count ? newArray[i] : null;

            if (!JToken.DeepEquals(oldValue, newValue))
            {
                diff.Add(new JObject
                {
                    ["old"] = oldValue,
                    ["new"] = newValue
                });
            }
        }

        return diff;
    }

    private async Task<string> GenerateKnowledgeChangeBriefAsync(string query, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new ()
                {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一個善於分析數據的助手，專門用於對內容變更進行簡要概括。請根據提供的變更內容，生成不超过 10 字的簡短總結，只需點明變更重點，無需過多解釋。")
                },
                new ()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input: {query}, output:")
                }
            },
            Model = OpenAiModel.Gpt4oMini
        }, cancellationToken).ConfigureAwait(false);

        return completionResult?.Data?.Response;
    }

    private record SceneComparableItem(string Name, KnowledgeSceneItemType Type, string Content, string FileName);
    
    public async Task<GetAgentKnowledgeResponse> GetAgentKnowledgeAsync(GetAgentKnowledgeRequest request, CancellationToken cancellationToken)
    {
        if (request.StoreId <= 0)
            throw new Exception("StoreId is required.");

        var sources = await _knowledgeScenarioDataProvider.GetAgentKnowledgeAsync(request.StoreId, request.Keyword, cancellationToken).ConfigureAwait(false);

        var result = sources
            .GroupBy(x => new { x.AgentId, x.AgentName })
            .Select(agentGroup => new GetAgentKnowledgeResponseData
            {
                AgentId = agentGroup.Key.AgentId,
                AgentName = agentGroup.Key.AgentName,
                Assistants = agentGroup
                    .GroupBy(x => new { x.AssistantId, x.AssistantName })
                    .Select(assistantGroup => new AssistantDto
                    {
                        AssistantId = assistantGroup.Key.AssistantId,
                        AssistantName = assistantGroup.Key.AssistantName
                    })
                    .OrderBy(x => x.AssistantName)
                    .ToList()
            })
            .OrderBy(x => x.AgentName)
            .ToList();

        return new GetAgentKnowledgeResponse
        {
            Data = result
        };
    }
}
