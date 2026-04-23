using AutoMapper;
using SmartTalk.Core.Services.AiSpeechAssistant;
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

    Task<AddKnowledgeSceneKnowledgeResponse> AddKnowledgeSceneKnowledgeAsync(AddKnowledgeSceneKnowledgeCommand command, CancellationToken cancellationToken);

    Task<UpdateKnowledgeSceneKnowledgeResponse> UpdateKnowledgeSceneKnowledgeAsync(UpdateKnowledgeSceneKnowledgeCommand command, CancellationToken cancellationToken);

    Task<DeleteKnowledgeSceneKnowledgeResponse> DeleteKnowledgeSceneKnowledgeAsync(DeleteKnowledgeSceneKnowledgeCommand command, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneFoldersResponse> GetKnowledgeSceneFoldersAsync(GetKnowledgeSceneFoldersRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeScenesResponse> GetKnowledgeScenesAsync(GetKnowledgeScenesRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneResponse> GetKnowledgeSceneAsync(GetKnowledgeSceneRequest request, CancellationToken cancellationToken);

    Task<GetKnowledgeSceneKnowledgesResponse> GetKnowledgeSceneKnowledgesAsync(GetKnowledgeSceneKnowledgesRequest request, CancellationToken cancellationToken);
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
        var knowledges = await _knowledgeScenarioDataProvider.GetKnowledgeSceneKnowledgesBySceneIdsAsync(sceneIds, cancellationToken).ConfigureAwait(false);
        var relations = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeSceneRelationsBySceneIdsAsync(sceneIds, cancellationToken).ConfigureAwait(false);
        var knowledgeIds = relations.Select(x => x.KnowledgeId).Distinct().ToList();

        if (relations.Count != 0)
            await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantKnowledgeSceneRelationsAsync(relations, false, cancellationToken).ConfigureAwait(false);

        if (knowledges.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneKnowledgesAsync(knowledges, false, cancellationToken).ConfigureAwait(false);

        if (scenes.Count != 0)
            await _knowledgeScenarioDataProvider.DeleteKnowledgeScenesAsync(scenes, false, cancellationToken).ConfigureAwait(false);

        await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneFolderAsync(folder, cancellationToken: cancellationToken).ConfigureAwait(false);
        //更新场景的prompt 等待review
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);

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

        var trimmedName = command.Name.Trim();
        var duplicatedScene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByFolderAndNameAsync(command.FolderId, trimmedName, cancellationToken).ConfigureAwait(false);

        if (duplicatedScene != null)
            throw new Exception($"AddKnowledgeScene Scene [{trimmedName}] already exists in this folder.");

        var scene = _mapper.Map<KnowledgeScene>(command);

        await _knowledgeScenarioDataProvider.AddKnowledgeSceneAsync(scene, true, cancellationToken: cancellationToken).ConfigureAwait(false);

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

        var trimmedName = command.Name.Trim();
        var duplicatedScene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByFolderAndNameAsync(command.FolderId, trimmedName, cancellationToken).ConfigureAwait(false);

        if (duplicatedScene != null && duplicatedScene.Id != command.Id)
            throw new Exception($"UpdateKnowledgeScene Scene [{trimmedName}] already exists in this folder.");

        _mapper.Map(command, scene);
        scene.UpdatedAt = DateTimeOffset.UtcNow;

        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneAsync(scene, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsBySceneIdsAsync([scene.Id], cancellationToken).ConfigureAwait(false);

        return new UpdateKnowledgeSceneResponse
        {
            Data = _mapper.Map<KnowledgeSceneDto>(scene)
        };
    }

    public async Task<AddKnowledgeSceneKnowledgeResponse> AddKnowledgeSceneKnowledgeAsync(AddKnowledgeSceneKnowledgeCommand command, CancellationToken cancellationToken)
    {
        if (command.SceneId <= 0) throw new Exception("AddKnowledgeSceneKnowledge SceneId is required.");

        if (string.IsNullOrWhiteSpace(command.Name)) throw new Exception("AddKnowledgeSceneKnowledge Name is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(command.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"AddKnowledgeSceneKnowledge Scene [{command.SceneId}] does not exist.");

        var trimmedName = command.Name.Trim();
        var duplicatedKnowledge = await _knowledgeScenarioDataProvider.GetKnowledgeSceneKnowledgeBySceneAndNameAsync(command.SceneId, trimmedName, cancellationToken).ConfigureAwait(false);

        if (duplicatedKnowledge != null)
            throw new Exception($"AddKnowledgeSceneKnowledge Knowledge [{trimmedName}] already exists in this scene.");

        var knowledge = _mapper.Map<KnowledgeSceneKnowledge>(command);
        var now = DateTimeOffset.UtcNow;

        await _knowledgeScenarioDataProvider.AddKnowledgeSceneKnowledgeAsync(knowledge, false, cancellationToken).ConfigureAwait(false);

        scene.UpdatedAt = now;
        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneAsync(scene, true, cancellationToken).ConfigureAwait(false);
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsBySceneIdsAsync([scene.Id], cancellationToken).ConfigureAwait(false);

        return new AddKnowledgeSceneKnowledgeResponse
        {
            Data = _mapper.Map<KnowledgeSceneKnowledgeDto>(knowledge)
        };
    }

    public async Task<UpdateKnowledgeSceneKnowledgeResponse> UpdateKnowledgeSceneKnowledgeAsync(UpdateKnowledgeSceneKnowledgeCommand command, CancellationToken cancellationToken)
    {
        if (command.Id <= 0) throw new Exception("UpdateKnowledgeSceneKnowledge Id is required.");

        if (string.IsNullOrWhiteSpace(command.Name)) throw new Exception("UpdateKnowledgeSceneKnowledge Name is required.");

        var knowledge = await _knowledgeScenarioDataProvider.GetKnowledgeSceneKnowledgeByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (knowledge == null)
            throw new Exception($"UpdateKnowledgeSceneKnowledge Knowledge [{command.Id}] does not exist.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(knowledge.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"UpdateKnowledgeSceneKnowledge Scene [{knowledge.SceneId}] does not exist.");

        var trimmedName = command.Name.Trim();
        var duplicatedKnowledge = await _knowledgeScenarioDataProvider.GetKnowledgeSceneKnowledgeBySceneAndNameAsync(knowledge.SceneId, trimmedName, cancellationToken).ConfigureAwait(false);

        if (duplicatedKnowledge != null && duplicatedKnowledge.Id != command.Id)
            throw new Exception($"UpdateKnowledgeSceneKnowledge Knowledge [{trimmedName}] already exists in this scene.");

        _mapper.Map(command, knowledge);
        var now = DateTimeOffset.UtcNow;
        knowledge.UpdatedAt = now;

        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneKnowledgeAsync(knowledge, false, cancellationToken).ConfigureAwait(false);

        scene.UpdatedAt = now;
        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneAsync(scene, true, cancellationToken).ConfigureAwait(false);
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsBySceneIdsAsync([scene.Id], cancellationToken).ConfigureAwait(false);

        return new UpdateKnowledgeSceneKnowledgeResponse
        {
            Data = _mapper.Map<KnowledgeSceneKnowledgeDto>(knowledge)
        };
    }

    public async Task<DeleteKnowledgeSceneKnowledgeResponse> DeleteKnowledgeSceneKnowledgeAsync(DeleteKnowledgeSceneKnowledgeCommand command, CancellationToken cancellationToken)
    {
        if (command.Id <= 0) throw new Exception("DeleteKnowledgeSceneKnowledge Id is required.");

        var knowledge = await _knowledgeScenarioDataProvider.GetKnowledgeSceneKnowledgeByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (knowledge == null)
            throw new Exception($"DeleteKnowledgeSceneKnowledge Knowledge [{command.Id}] does not exist.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(knowledge.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"DeleteKnowledgeSceneKnowledge Scene [{knowledge.SceneId}] does not exist.");

        await _knowledgeScenarioDataProvider.DeleteKnowledgeSceneKnowledgeAsync(knowledge, false, cancellationToken).ConfigureAwait(false);

        scene.UpdatedAt = DateTimeOffset.UtcNow;
        await _knowledgeScenarioDataProvider.UpdateKnowledgeSceneAsync(scene, true, cancellationToken).ConfigureAwait(false);
        await _aiSpeechAssistantKnowledgePromptService.RefreshScenePromptsBySceneIdsAsync([scene.Id], cancellationToken).ConfigureAwait(false);

        return new DeleteKnowledgeSceneKnowledgeResponse
        {
            Data = _mapper.Map<KnowledgeSceneKnowledgeDto>(knowledge)
        };
    }

    public async Task<GetKnowledgeSceneFoldersResponse> GetKnowledgeSceneFoldersAsync(GetKnowledgeSceneFoldersRequest request, CancellationToken cancellationToken)
    {
        var folders = await _knowledgeScenarioDataProvider.GetKnowledgeSceneFoldersAsync(request.Keyword, cancellationToken).ConfigureAwait(false);

        return new GetKnowledgeSceneFoldersResponse
        {
            Data = _mapper.Map<List<KnowledgeSceneFolderDto>>(folders)
        };
    }

    public async Task<GetKnowledgeScenesResponse> GetKnowledgeScenesAsync(GetKnowledgeScenesRequest request, CancellationToken cancellationToken)
    {
        if (request.FolderId <= 0) throw new Exception("GetKnowledgeScenes FolderId is required.");

        var scenes = await _knowledgeScenarioDataProvider.GetKnowledgeScenesAsync(request.FolderId, request.Keyword, cancellationToken).ConfigureAwait(false);

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

        var knowledges = await _knowledgeScenarioDataProvider.GetKnowledgeSceneKnowledgesAsync(request.Id, null, cancellationToken).ConfigureAwait(false);
        var sceneDto = _mapper.Map<KnowledgeSceneDetailDto>(scene);
        sceneDto.Knowledges = _mapper.Map<List<KnowledgeSceneKnowledgeDto>>(knowledges);

        return new GetKnowledgeSceneResponse
        {
            Data = sceneDto
        };
    }

    public async Task<GetKnowledgeSceneKnowledgesResponse> GetKnowledgeSceneKnowledgesAsync(GetKnowledgeSceneKnowledgesRequest request, CancellationToken cancellationToken)
    {
        if (request.SceneId <= 0) throw new Exception("GetKnowledgeSceneKnowledges SceneId is required.");

        var scene = await _knowledgeScenarioDataProvider.GetKnowledgeSceneByIdAsync(request.SceneId, cancellationToken).ConfigureAwait(false);

        if (scene == null)
            throw new Exception($"GetKnowledgeSceneKnowledges Scene [{request.SceneId}] does not exist.");

        var knowledges = await _knowledgeScenarioDataProvider.GetKnowledgeSceneKnowledgesAsync(request.SceneId, request.Keyword, cancellationToken).ConfigureAwait(false);

        return new GetKnowledgeSceneKnowledgesResponse
        {
            Data = _mapper.Map<List<KnowledgeSceneKnowledgeDto>>(knowledges)
        };
    }
}
