using AutoMapper;
using Serilog;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.KnowledgeScenario;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Requests.KnowledgeScenario;

namespace SmartTalk.Core.Services.KnowledgeScenario;

public interface IKnowledgeScenarioService : IScopedDependency
{
    Task<AddKnowledgeSceneFolderResponse> AddKnowledgeSceneFolderAsync(AddKnowledgeSceneFolderCommand command, CancellationToken cancellationToken);

    Task<UpdateKnowledgeSceneFolderResponse> UpdateKnowledgeSceneFolderAsync(UpdateKnowledgeSceneFolderCommand command, CancellationToken cancellationToken);

    Task<DeleteKnowledgeSceneFolderResponse> DeleteKnowledgeSceneFolderAsync(DeleteKnowledgeSceneFolderCommand command, CancellationToken cancellationToken);

    Task<AddKnowledgeSceneResponse> AddKnowledgeSceneAsync(AddKnowledgeSceneCommand command, CancellationToken cancellationToken);

    Task<UpdateKnowledgeSceneResponse> UpdateKnowledgeSceneAsync(UpdateKnowledgeSceneCommand command, CancellationToken cancellationToken);

    Task<AddKnowledgeSceneItemResponse> AddKnowledgeSceneItemAsync(AddKnowledgeSceneItemCommand command, CancellationToken cancellationToken);

    Task<UpdateKnowledgeSceneItemResponse> UpdateKnowledgeSceneItemAsync(UpdateKnowledgeSceneItemCommand command, CancellationToken cancellationToken);

    Task<DeleteKnowledgeSceneItemResponse> DeleteKnowledgeSceneItemAsync(DeleteKnowledgeSceneItemCommand command, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneFoldersResponse> GetKnowledgeSceneFoldersAsync(GetKnowledgeSceneFoldersRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeScenesResponse> GetKnowledgeScenesAsync(GetKnowledgeScenesRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneResponse> GetKnowledgeSceneAsync(GetKnowledgeSceneRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneItemsResponse> GetKnowledgeSceneItemsAsync(GetKnowledgeSceneItemsRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneRelatedKnowledgesResponse> GetKnowledgeSceneRelatedKnowledgesAsync(GetKnowledgeSceneRelatedKnowledgesRequest request, CancellationToken cancellationToken);

    Task<SaveKnowledgeSceneRelatedKnowledgesResponse> SaveKnowledgeSceneRelatedKnowledgesAsync(SaveKnowledgeSceneRelatedKnowledgesCommand command, CancellationToken cancellationToken);
}

public class KnowledgeScenarioService : IKnowledgeScenarioService
{
    private readonly IMapper _mapper;
    private readonly IAiSpeechAssistantKnowledgePromptService _aiSpeechAssistantKnowledgePromptService;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IKnowledgeScenarioDataProvider _knowledgeScenarioDataProvider;

    public KnowledgeScenarioService(
        IMapper mapper,
        IKnowledgeScenarioDataProvider knowledgeScenarioDataProvider,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        IAiSpeechAssistantKnowledgePromptService aiSpeechAssistantKnowledgePromptService)
    {
        _mapper = mapper;
        _knowledgeScenarioDataProvider = knowledgeScenarioDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
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

        await _knowledgeScenarioDataProvider.AddKnowledgeSceneAsync(scene, true, cancellationToken: cancellationToken).ConfigureAwait(false);
        Log.Information("AddKnowledgeSceneAsync completed. SceneId={@SceneId}, FolderId={@FolderId}, Status={@Status}", scene.Id, scene.FolderId, scene.Status);

        return new AddKnowledgeSceneResponse
        {
            Data = _mapper.Map<KnowledgeSceneDto>(scene)
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

        _mapper.Map(command, scene);
        scene.UpdatedAt = DateTimeOffset.UtcNow;

        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneAsync(scene, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (command.Status.HasValue)
        {
            Log.Information("UpdateKnowledgeSceneAsync status changed. SceneId={@SceneId}, SceneStatus={@SceneStatus}. Refreshing related prompts.", scene.Id, scene.Status);
            await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsBySceneIdsAsync([scene.Id], cancellationToken).ConfigureAwait(false);
        }
        Log.Information("UpdateKnowledgeSceneAsync updated scene. SceneId={@SceneId}, SceneFolderId={@SceneFolderId}, SceneName={@SceneName}", scene.Id, scene.FolderId, scene.Name);

        return new UpdateKnowledgeSceneResponse
        {
            Data = _mapper.Map<KnowledgeSceneDto>(scene)
        };
    }

    public async Task<AddKnowledgeSceneItemResponse> AddKnowledgeSceneItemAsync(AddKnowledgeSceneItemCommand command, CancellationToken cancellationToken)
    {
        if (command.SceneId <= 0) throw new Exception("AddKnowledgeSceneItem SceneId is required.");

        if (command.Items == null || command.Items.Count == 0) throw new Exception("AddKnowledgeSceneItem Items is required.");

        var itemNames = new List<string>();
        foreach (var item in command.Items)
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

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(command.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"AddKnowledgeSceneItem Scene [{command.SceneId}] does not exist.");

        var duplicatedKnowledges = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemsBySceneAndNamesAsync(command.SceneId, itemNames, cancellationToken).ConfigureAwait(false);

        if (duplicatedKnowledges.Count != 0)
            throw new Exception($"AddKnowledgeSceneItem Items [{string.Join(", ", duplicatedKnowledges.Select(x => x.Name))}] already exist in this scene.");

        var knowledges = command.Items.Select(item =>
        {
            var knowledge = _mapper.Map<KnowledgeSceneItem>(item);
            knowledge.SceneId = command.SceneId;
            return knowledge;
        }).ToList();
        var now = DateTimeOffset.UtcNow;

        await _knowledgeScenarioDataProvider.AddKnowledgeSceneItemsAsync(knowledges, false, cancellationToken).ConfigureAwait(false);

        scene.UpdatedAt = now;
        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneAsync(scene, true, cancellationToken).ConfigureAwait(false);
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsBySceneIdsAsync([scene.Id], cancellationToken).ConfigureAwait(false);
        
        Log.Information("AddKnowledgeSceneItemAsync added scene items and refreshed prompts. SceneId={@SceneId}, ItemIds={@ItemIds}", 
            scene.Id, knowledges.Select(x => x.Id).ToList());

        return new AddKnowledgeSceneItemResponse
        {
            Data = _mapper.Map<List<KnowledgeSceneItemDto>>(knowledges)
        };
    }

    public async Task<UpdateKnowledgeSceneItemResponse> UpdateKnowledgeSceneItemAsync(UpdateKnowledgeSceneItemCommand command, CancellationToken cancellationToken)
    {
        if (command.Id <= 0) throw new Exception("UpdateKnowledgeSceneItem Id is required.");

        if (string.IsNullOrWhiteSpace(command.Name)) throw new Exception("UpdateKnowledgeSceneItem Name is required.");

        var knowledge = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (knowledge == null)
            throw new Exception($"UpdateKnowledgeSceneItem Item [{command.Id}] does not exist.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(knowledge.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"UpdateKnowledgeSceneItem Scene [{knowledge.SceneId}] does not exist.");

        var trimmedName = command.Name.Trim();
        var duplicatedKnowledge = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemBySceneAndNameAsync(knowledge.SceneId, trimmedName, cancellationToken).ConfigureAwait(false);

        if (duplicatedKnowledge != null && duplicatedKnowledge.Id != command.Id)
            throw new Exception($"UpdateKnowledgeSceneItem Item [{trimmedName}] already exists in this scene.");

        _mapper.Map(command, knowledge);
        var now = DateTimeOffset.UtcNow;
        knowledge.UpdatedAt = now;

        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneItemAsync(knowledge, false, cancellationToken).ConfigureAwait(false);

        scene.UpdatedAt = now;
        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneAsync(scene, true, cancellationToken).ConfigureAwait(false);
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsBySceneIdsAsync([scene.Id], cancellationToken).ConfigureAwait(false);
        
        Log.Information("UpdateKnowledgeSceneItemAsync updated scene item and refreshed prompts. ItemId={ItemId}, SceneId={SceneId},  KnowledgeName={KnowledgeName}, KnowledgeType={KnowledgeType}",
            knowledge.Id, knowledge.SceneId, knowledge.Name, knowledge.Type);

        return new UpdateKnowledgeSceneItemResponse
        {
            Data = _mapper.Map<KnowledgeSceneItemDto>(knowledge)
        };
    }

    public async Task<DeleteKnowledgeSceneItemResponse> DeleteKnowledgeSceneItemAsync(DeleteKnowledgeSceneItemCommand command, CancellationToken cancellationToken)
    {
        if (command.Id <= 0) throw new Exception("DeleteKnowledgeSceneItem Id is required.");

        var knowledge = await _knowledgeScenarioDataProvider.GetKnowledgeSceneItemByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (knowledge == null)
            throw new Exception($"DeleteKnowledgeSceneItem Item [{command.Id}] does not exist.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(knowledge.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"DeleteKnowledgeSceneItem Scene [{knowledge.SceneId}] does not exist.");

        await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneItemAsync(knowledge, false, cancellationToken).ConfigureAwait(false);

        scene.UpdatedAt = DateTimeOffset.UtcNow;
        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneAsync(scene, true, cancellationToken).ConfigureAwait(false);
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsBySceneIdsAsync([scene.Id], cancellationToken).ConfigureAwait(false);
        
        Log.Information("DeleteKnowledgeSceneItemAsync removed scene item and refreshed prompts. ItemId={ItemId}, SceneId={SceneId}, ItemType={ItemType}",
            knowledge.Id, scene.Id, knowledge.Type);

        return new DeleteKnowledgeSceneItemResponse
        {
            Data = _mapper.Map<KnowledgeSceneItemDto>(knowledge)
        };
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
        
        Log.Information("GetKnowledgeScenesAsync completed. FolderId={FolderId}, SceneCount={Count}", request.FolderId, scenes.Count);

        return new GetKnowledgeScenesResponse
        {
            Data = _mapper.Map<List<KnowledgeSceneDto>>(scenes)
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
            Data = _mapper.Map<List<KnowledgeSceneItemDto>>(knowledges)
        };
    }

    public async Task<GetKnowledgeSceneRelatedKnowledgesResponse> GetKnowledgeSceneRelatedKnowledgesAsync(GetKnowledgeSceneRelatedKnowledgesRequest request, CancellationToken cancellationToken)
    {
        if (request.SceneId <= 0) throw new Exception("GetKnowledgeSceneRelatedKnowledges SceneId is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(request.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"GetKnowledgeSceneRelatedKnowledges Scene [{request.SceneId}] does not exist.");

        var relations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdAsync(request.SceneId, cancellationToken).ConfigureAwait(false);
        var data = await BuildRelatedKnowledgeDtosAsync(relations, cancellationToken).ConfigureAwait(false);
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

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(command.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"SaveKnowledgeSceneRelatedKnowledges Scene [{command.SceneId}] does not exist.");

        var targetKnowledgeIds = command.KnowledgeIds.Distinct().ToList();
        
        var currentRelations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdAsync(command.SceneId, cancellationToken).ConfigureAwait(false);
        var currentKnowledgeIds = currentRelations.Select(x => x.KnowledgeId).Distinct().ToHashSet();
        var targetKnowledgeIdSet = targetKnowledgeIds.ToHashSet();

        var relationsToDelete = currentRelations.Where(x => !targetKnowledgeIdSet.Contains(x.KnowledgeId)).ToList();
        var knowledgeIdsToAdd = targetKnowledgeIds.Where(x => !currentKnowledgeIds.Contains(x)).ToList(); 
        
        Log.Information("SaveKnowledgeSceneRelatedKnowledgesAsync diff built. SceneId={SceneId}, CurrentCount={CurrentCount}, TargetCount={TargetCount}, DeleteCount={DeleteCount}, AddCount={AddCount}",
            command.SceneId, currentKnowledgeIds.Count, targetKnowledgeIds.Count, relationsToDelete.Count, knowledgeIdsToAdd.Count);

        if (relationsToDelete.Count != 0)
            await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(relationsToDelete, true, cancellationToken).ConfigureAwait(false);

        foreach (var knowledgeId in knowledgeIdsToAdd)
        {
            var relation = new AiSpeechAssistantKnowledgeSceneRelation
            {
                KnowledgeId = knowledgeId,
                SceneId = command.SceneId
            };

            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgeSceneRelationAsync(relation, true, cancellationToken).ConfigureAwait(false);
        }

        var affectedKnowledgeIds = currentKnowledgeIds.Union(targetKnowledgeIdSet).ToList();

        if (affectedKnowledgeIds.Count != 0)
        {
            Log.Information("SaveKnowledgeSceneRelatedKnowledgesAsync refresh prompts. SceneId={SceneId}, AffectedKnowledgeCount={AffectedKnowledgeCount}", command.SceneId, affectedKnowledgeIds.Count);
            await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsAsync(affectedKnowledgeIds, cancellationToken).ConfigureAwait(false);
        }

        var latestRelations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdAsync(command.SceneId, cancellationToken).ConfigureAwait(false);
        var data = await BuildRelatedKnowledgeDtosAsync(latestRelations, cancellationToken).ConfigureAwait(false);
        Log.Information("SaveKnowledgeSceneRelatedKnowledgesAsync completed. SceneId={SceneId}, SavedKnowledgeCount={KnowledgeCount}", command.SceneId, data.Count);

        return new SaveKnowledgeSceneRelatedKnowledgesResponse
        {
            Data = data
        };
    }

    private async Task<List<int>> BuildRelatedKnowledgeDtosAsync(List<AiSpeechAssistantKnowledgeSceneRelation> relations, CancellationToken cancellationToken)
    {
        if (relations.Count == 0)
            return [];

        var relationMap = relations
            .GroupBy(x => x.KnowledgeId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.CreatedAt).First());
        var knowledgeIds = relationMap.Keys.ToList();
        var knowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);

        return knowledges
            .Where(x => relationMap.ContainsKey(x.Id))
            .OrderByDescending(x => relationMap[x.Id].CreatedAt)
            .Select(x => x.Id)
            .ToList();
    }
}
