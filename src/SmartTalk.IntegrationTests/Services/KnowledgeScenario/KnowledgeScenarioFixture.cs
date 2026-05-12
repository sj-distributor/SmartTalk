using Autofac;
using Mediator.Net;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.IntegrationTests.TestBaseClasses;
using SmartTalk.Messages.Commands.KnowledgeScenario;
using SmartTalk.Messages.Dto.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Requests.KnowledgeScenario;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.KnowledgeScenario;

public class KnowledgeScenarioFixture : KnowledgeScenarioFixtureBase
{
    [Fact]
    public async Task ShouldGetKnowledgeSceneFolders()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            await CreateFolderAsync(repository, unitOfWork, $"Folder-Match-{caseId}");
            await CreateFolderAsync(repository, unitOfWork, $"Folder-Other-{caseId}");
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetKnowledgeSceneFoldersRequest, GetKnowledgeSceneFoldersResponse>(
                new GetKnowledgeSceneFoldersRequest { Keyword = $"Match-{caseId}" });

            response.ShouldNotBeNull();
            response.Data.Count.ShouldBe(1);
            response.Data.Single().Name.ShouldBe($"Folder-Match-{caseId}");
        });
    }

    [Fact]
    public async Task ShouldAddKnowledgeSceneFolder()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        AddKnowledgeSceneFolderResponse response = null!;

        await Run<IMediator>(async mediator =>
        {
            response = await mediator.SendAsync<AddKnowledgeSceneFolderCommand, AddKnowledgeSceneFolderResponse>(new AddKnowledgeSceneFolderCommand { Name = $"Folder-{caseId}" });
        });

        response.Data.ShouldNotBeNull();
        response.Data.Name.ShouldBe($"Folder-{caseId}");

        await Run<IRepository>(async repository =>
        {
            var folder = await repository.Query<KnowledgeSceneFolder>().FirstOrDefaultAsync(x => x.Id == response.Data.Id);

            folder.ShouldNotBeNull();
            folder.Name.ShouldBe($"Folder-{caseId}");
        });
    }

    [Fact]
    public async Task ShouldUpdateKnowledgeSceneFolder()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        int folderId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            folderId = (await CreateFolderAsync(repository, unitOfWork, $"Folder-{caseId}")).Id;
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.SendAsync<UpdateKnowledgeSceneFolderCommand, UpdateKnowledgeSceneFolderResponse>(
                new UpdateKnowledgeSceneFolderCommand
                {
                    Id = folderId,
                    Name = $"Folder-Updated-{caseId}"
                });

            response.Data.ShouldNotBeNull();
            response.Data.Id.ShouldBe(folderId);
            response.Data.Name.ShouldBe($"Folder-Updated-{caseId}");
        });

        await Run<IRepository>(async repository =>
        {
            var folder = await repository.Query<KnowledgeSceneFolder>().FirstOrDefaultAsync(x => x.Id == folderId);

            folder.ShouldNotBeNull();
            folder.Name.ShouldBe($"Folder-Updated-{caseId}");
            folder.UpdatedAt.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task ShouldDeleteKnowledgeSceneFolder()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        FolderCascadeSeedResult seeded = null!;
        var promptService = CreatePromptServiceMock();

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            seeded = await SeedFolderCascadeAsync(repository, unitOfWork, caseId);
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.SendAsync<DeleteKnowledgeSceneFolderCommand, DeleteKnowledgeSceneFolderResponse>(
                new DeleteKnowledgeSceneFolderCommand { Id = seeded.FolderId });

            response.Data.ShouldNotBeNull();
            response.Data.Id.ShouldBe(seeded.FolderId);
            response.Data.Name.ShouldBe(seeded.FolderName);
        }, BuildPromptServiceRegistration(promptService));

        await Run<IRepository>(async repository =>
        {
            (await repository.Query<KnowledgeSceneFolder>().AnyAsync(x => x.Id == seeded.FolderId)).ShouldBeFalse();
            (await repository.Query<KnowledgeScene>().AnyAsync(x => x.Id == seeded.SceneId)).ShouldBeFalse();
            (await repository.Query<KnowledgeSceneItem>().AnyAsync(x => x.Id == seeded.SceneItemId)).ShouldBeFalse();
            (await repository.Query<AiSpeechAssistantKnowledgeSceneRelation>().AnyAsync(x => x.Id == seeded.RelationId)).ShouldBeFalse();
        });

        await promptService.Received(1).RefreshScenePromptsAsync(Arg.Is<List<int>>(ids => ids.Count == 1 && ids[0] == seeded.KnowledgeId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShouldGetKnowledgeScenes()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        int folderId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var folder = await CreateFolderAsync(repository, unitOfWork, $"Folder-{caseId}");
            folderId = folder.Id;
            await CreateSceneAsync(repository, unitOfWork, folderId, $"Scene-Match-{caseId}", "desc", KnowledgeSceneStatus.Published);
            await CreateSceneAsync(repository, unitOfWork, folderId, $"Scene-Other-{caseId}", "desc", KnowledgeSceneStatus.OffShelf);
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetKnowledgeScenesRequest, GetKnowledgeScenesResponse>(
                new GetKnowledgeScenesRequest
                {
                    FolderId = folderId,
                    Keyword = $"Match-{caseId}"
                });

            response.ShouldNotBeNull();
            response.Data.Count.ShouldBe(1);
            response.Data.Single().Name.ShouldBe($"Scene-Match-{caseId}");
        });
    }

    [Fact]
    public async Task ShouldGetKnowledgeScene()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        int sceneId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var folder = await CreateFolderAsync(repository, unitOfWork, $"Folder-{caseId}");
            var scene = await CreateSceneAsync(repository, unitOfWork, folder.Id, $"Scene-{caseId}", $"Scene Desc {caseId}", KnowledgeSceneStatus.Published);
            sceneId = scene.Id;

            await CreateSceneItemAsync(repository, unitOfWork, sceneId, $"Item-A-{caseId}", KnowledgeSceneItemType.Text, $"Content A {caseId}");
            await CreateSceneItemAsync(repository, unitOfWork, sceneId, $"Item-B-{caseId}", KnowledgeSceneItemType.File, $"Content B {caseId}", $"file-{caseId}.pdf");
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetKnowledgeSceneRequest, GetKnowledgeSceneResponse>(
                new GetKnowledgeSceneRequest { Id = sceneId });

            response.ShouldNotBeNull();
            response.Data.ShouldNotBeNull();
            response.Data.SceneData.ShouldNotBeNull();
            response.Data.SceneData.Id.ShouldBe(sceneId);
            response.Data.SceneData.Name.ShouldBe($"Scene-{caseId}");
            response.Data.SceneData.SceneItems.Count.ShouldBe(2);
            response.Data.SceneData.SceneItems.Any(x => x.Name == $"Item-A-{caseId}").ShouldBeTrue();
            response.Data.SceneData.SceneItems.Any(x => x.Name == $"Item-B-{caseId}" && x.FileName == $"file-{caseId}.pdf").ShouldBeTrue();
        });
    }

    [Fact]
    public async Task ShouldAddKnowledgeScene()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        int folderId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            folderId = (await CreateFolderAsync(repository, unitOfWork, $"Folder-{caseId}")).Id;
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.SendAsync<AddKnowledgeSceneCommand, AddKnowledgeSceneResponse>(
                new AddKnowledgeSceneCommand
                {
                    FolderId = folderId,
                    Name = $"  Scene-{caseId}  ",
                    Description = $"  Scene Desc {caseId}  ",
                    Status = KnowledgeSceneStatus.Published,
                    SceneItems =
                    [
                        new KnowledgeSceneItemDto
                        {
                            Name = $"  Item-{caseId}  ",
                            Type = KnowledgeSceneItemType.Text,
                            Content = $"  Content {caseId}  "
                        },
                        new KnowledgeSceneItemDto
                        {
                            Name = $"  Item-File-{caseId}  ",
                            Type = KnowledgeSceneItemType.File,
                            FileName = $"  file-{caseId}.pdf  "
                        }
                    ]
                });

            response.Data.ShouldNotBeNull();
            response.Data.FolderId.ShouldBe(folderId);
            response.Data.Name.ShouldBe($"Scene-{caseId}");
            response.Data.Description.ShouldBe($"Scene Desc {caseId}");
            response.Data.Status.ShouldBe(KnowledgeSceneStatus.Published);
            response.Data.SceneItems.Count.ShouldBe(2);
            response.Data.SceneItems[0].SceneId.ShouldBe(response.Data.Id);
            response.Data.SceneItems[0].Name.ShouldBe($"Item-{caseId}");
            response.Data.SceneItems[0].Content.ShouldBe($"Content {caseId}");
            response.Data.SceneItems[1].SceneId.ShouldBe(response.Data.Id);
            response.Data.SceneItems[1].Name.ShouldBe($"Item-File-{caseId}");
            response.Data.SceneItems[1].FileName.ShouldBe($"file-{caseId}.pdf");
        });
    }

    [Fact]
    public async Task ShouldUpdateKnowledgeScene()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        int folderId = 0;
        int sceneId = 0;
        int itemId = 0;
        var promptService = CreatePromptServiceMock();

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var folder = await CreateFolderAsync(repository, unitOfWork, $"Folder-{caseId}");
            folderId = folder.Id;
            sceneId = (await CreateSceneAsync(repository, unitOfWork, folderId, $"Scene-{caseId}", "desc", KnowledgeSceneStatus.OffShelf)).Id;
            itemId = (await CreateSceneItemAsync(repository, unitOfWork, sceneId, $"Item-{caseId}", KnowledgeSceneItemType.Text, $"Content {caseId}")).Id;
            await CreateSceneItemAsync(repository, unitOfWork, sceneId, $"Item-Delete-{caseId}", KnowledgeSceneItemType.Text, $"Delete Content {caseId}");
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.SendAsync<UpdateKnowledgeSceneCommand, UpdateKnowledgeSceneResponse>(
                new UpdateKnowledgeSceneCommand
                {
                    Id = sceneId,
                    FolderId = folderId,
                    Name = $"  Scene-Updated-{caseId}  ",
                    Description = $"  Updated Desc {caseId}  ",
                    Status = KnowledgeSceneStatus.Published,
                    SceneItems =
                    [
                        new KnowledgeSceneItemDto
                        {
                            Id = itemId,
                            Name = $"  Item-Updated-{caseId}  ",
                            Type = KnowledgeSceneItemType.FAQ,
                            Content = $"  Updated Content {caseId}  ",
                            FileName = $"  updated-{caseId}.txt  "
                        },
                        new KnowledgeSceneItemDto
                        {
                            Name = $"  Item-New-{caseId}  ",
                            Type = KnowledgeSceneItemType.Text,
                            Content = $"  New Content {caseId}  "
                        }
                    ]
                });

            response.Data.ShouldNotBeNull();
            response.Data.Id.ShouldBe(sceneId);
            response.Data.Name.ShouldBe($"Scene-Updated-{caseId}");
            response.Data.Description.ShouldBe($"Updated Desc {caseId}");
            response.Data.Status.ShouldBe(KnowledgeSceneStatus.Published);
            response.Data.SceneItems.Count.ShouldBe(2);
            response.Data.SceneItems.Any(x => x.Id == itemId && x.Name == $"Item-Updated-{caseId}" && x.Type == KnowledgeSceneItemType.FAQ).ShouldBeTrue();
            response.Data.SceneItems.Any(x => x.Name == $"Item-New-{caseId}" && x.Content == $"New Content {caseId}").ShouldBeTrue();
        }, BuildPromptServiceRegistration(promptService));

        await Run<IRepository>(async repository =>
        {
            var scene = await repository.Query<KnowledgeScene>().FirstOrDefaultAsync(x => x.Id == sceneId);

            scene.ShouldNotBeNull();
            scene.Name.ShouldBe($"Scene-Updated-{caseId}");
            scene.Description.ShouldBe($"Updated Desc {caseId}");
            scene.Status.ShouldBe(KnowledgeSceneStatus.Published);
            scene.UpdatedAt.ShouldNotBeNull();
            var items = await repository.Query<KnowledgeSceneItem>().Where(x => x.SceneId == sceneId).ToListAsync();

            items.Count.ShouldBe(2);
            items.Any(x => x.Id == itemId && x.Name == $"Item-Updated-{caseId}" && x.Type == KnowledgeSceneItemType.FAQ).ShouldBeTrue();
            items.Any(x => x.Name == $"Item-New-{caseId}" && x.Content == $"New Content {caseId}").ShouldBeTrue();
            items.Any(x => x.Name == $"Item-Delete-{caseId}").ShouldBeFalse();
        });

        await promptService.Received(1).RefreshScenePromptsBySceneIdsAsync(Arg.Is<List<int>>(ids => ids.Count == 1 && ids[0] == sceneId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShouldGetKnowledgeSceneRelatedKnowledges()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        RelationSaveSeedResult seeded = null!;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            seeded = await SeedSceneRelationAsync(repository, unitOfWork, caseId);
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetKnowledgeSceneRelatedKnowledgesRequest, GetKnowledgeSceneRelatedKnowledgesResponse>(
                new GetKnowledgeSceneRelatedKnowledgesRequest { SceneId = seeded.SceneId });

            response.ShouldNotBeNull();
            response.Data.Count.ShouldBe(2);
            response.Data[0].ShouldBe(seeded.SecondKnowledgeId);
            response.Data[1].ShouldBe(seeded.FirstKnowledgeId);
        });
    }

    [Fact]
    public async Task ShouldSaveKnowledgeSceneRelatedKnowledges()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        RelationSaveSeedResult seeded = null!;
        var promptService = CreatePromptServiceMock();

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            seeded = await SeedSceneRelationAsync(repository, unitOfWork, caseId, includeSecondRelation: false);
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.SendAsync<SaveKnowledgeSceneRelatedKnowledgesCommand, SaveKnowledgeSceneRelatedKnowledgesResponse>(
                new SaveKnowledgeSceneRelatedKnowledgesCommand
                {
                    SceneId = seeded.SceneId,
                    KnowledgeIds = new List<int> { seeded.SecondKnowledgeId }
                });

            response.ShouldNotBeNull();
            response.Data.Count.ShouldBe(1);
            response.Data.Single().ShouldBe(seeded.SecondKnowledgeId);
        }, BuildPromptServiceRegistration(promptService));

        await Run<IRepository>(async repository =>
        {
            var relations = await repository.Query<AiSpeechAssistantKnowledgeSceneRelation>()
                .Where(x => x.SceneId == seeded.SceneId)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            relations.Count.ShouldBe(1);
            relations.Single().KnowledgeId.ShouldBe(seeded.SecondKnowledgeId);
        });

        await promptService.Received(1).RefreshScenePromptsAsync(Arg.Is<List<int>>(ids => ids.Count == 2
            && ids.Contains(seeded.FirstKnowledgeId)
            && ids.Contains(seeded.SecondKnowledgeId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShouldGetKnowledgeSceneItems()
    {
        var caseId = Guid.NewGuid().ToString("N")[..8];
        int sceneId = 0;

        await RunWithUnitOfWork<IRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var folder = await CreateFolderAsync(repository, unitOfWork, $"Folder-{caseId}");
            sceneId = (await CreateSceneAsync(repository, unitOfWork, folder.Id, $"Scene-{caseId}", "desc", KnowledgeSceneStatus.Published)).Id;

            await CreateSceneItemAsync(repository, unitOfWork, sceneId, $"Item-Match-{caseId}", KnowledgeSceneItemType.Text, $"Content {caseId}");
            await CreateSceneItemAsync(repository, unitOfWork, sceneId, $"Item-Other-{caseId}", KnowledgeSceneItemType.FAQ, $"FAQ {caseId}");
        });

        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetKnowledgeSceneItemsRequest, GetKnowledgeSceneItemsResponse>(
                new GetKnowledgeSceneItemsRequest
                {
                    SceneId = sceneId,
                    Keyword = $"Match-{caseId}"
                });

            response.ShouldNotBeNull();
            response.Data.Count.ShouldBe(1);
            response.Data.Single().Name.ShouldBe($"Item-Match-{caseId}");
        });
    }

    private static IAiSpeechAssistantKnowledgePromptService CreatePromptServiceMock()
    {
        var promptService = Substitute.For<IAiSpeechAssistantKnowledgePromptService>();
        promptService.RefreshScenePromptsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        promptService.RefreshScenePromptsBySceneIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        return promptService;
    }

    private static Action<ContainerBuilder> BuildPromptServiceRegistration(IAiSpeechAssistantKnowledgePromptService promptService)
    {
        return builder => builder.RegisterInstance(promptService).As<IAiSpeechAssistantKnowledgePromptService>();
    }

    private static async Task<KnowledgeSceneFolder> CreateFolderAsync(IRepository repository, IUnitOfWork unitOfWork, string name)
    {
        var folder = new KnowledgeSceneFolder
        {
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await repository.InsertAsync(folder);
        await unitOfWork.SaveChangesAsync();

        return folder;
    }

    private static async Task<KnowledgeScene> CreateSceneAsync(
        IRepository repository, IUnitOfWork unitOfWork, int folderId, string name, string description, KnowledgeSceneStatus status)
    {
        var scene = new KnowledgeScene
        {
            FolderId = folderId,
            Name = name,
            Description = description,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await repository.InsertAsync(scene);
        await unitOfWork.SaveChangesAsync();

        return scene;
    }

    private static async Task<KnowledgeSceneItem> CreateSceneItemAsync(IRepository repository, IUnitOfWork unitOfWork, int sceneId, 
        string name, KnowledgeSceneItemType type, string content, string fileName = "")
    {
        var item = new KnowledgeSceneItem
        {
            SceneId = sceneId,
            Name = name,
            Type = type,
            Content = content,
            FileName = fileName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await repository.InsertAsync(item);
        await unitOfWork.SaveChangesAsync();

        return item;
    }

    private static async Task<AiSpeechAssistantKnowledge> CreateActiveKnowledgeAsync(IRepository repository, IUnitOfWork unitOfWork, string suffix)
    {
        var assistant = new SmartTalk.Core.Domain.AISpeechAssistant.AiSpeechAssistant
        {
            Name = $"Assistant-{suffix}",
            AnsweringNumber = $"+1555{suffix[..Math.Min(6, suffix.Length)]}",
            ModelProvider = RealtimeAiProvider.OpenAi,
            ModelVoice = "alloy",
            IsDefault = true,
            IsDisplay = true,
            CreatedDate = DateTimeOffset.UtcNow,
            CreatedBy = 1,
            ModelLanguage = "English"
        };
        await repository.InsertAsync(assistant);
        await unitOfWork.SaveChangesAsync();

        var knowledge = new AiSpeechAssistantKnowledge
        {
            AssistantId = assistant.Id,
            Json = "{}",
            Prompt = $"Prompt-{suffix}",
            IsActive = true,
            Version = "1.0",
            Brief = $"Brief-{suffix}",
            Greetings = $"Greetings-{suffix}",
            CreatedDate = DateTimeOffset.UtcNow,
            CreatedBy = 1,
            ModelLanguage = "English"
        };
        await repository.InsertAsync(knowledge);
        await unitOfWork.SaveChangesAsync();

        return knowledge;
    }

    private static async Task<FolderCascadeSeedResult> SeedFolderCascadeAsync(IRepository repository, IUnitOfWork unitOfWork, string suffix)
    {
        var folder = await CreateFolderAsync(repository, unitOfWork, $"Folder-{suffix}");
        var scene = await CreateSceneAsync(repository, unitOfWork, folder.Id, $"Scene-{suffix}", $"Scene Desc {suffix}", KnowledgeSceneStatus.Published);
        var item = await CreateSceneItemAsync(repository, unitOfWork, scene.Id, $"Item-{suffix}", KnowledgeSceneItemType.Text, $"Content {suffix}");
        var knowledge = await CreateActiveKnowledgeAsync(repository, unitOfWork, $"Knowledge-{suffix}");

        var relation = new AiSpeechAssistantKnowledgeSceneRelation
        {
            KnowledgeId = knowledge.Id,
            SceneId = scene.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await repository.InsertAsync(relation);
        await unitOfWork.SaveChangesAsync();

        return new FolderCascadeSeedResult(folder.Id, folder.Name, scene.Id, item.Id, relation.Id, knowledge.Id);
    }

    private static async Task<RelationSaveSeedResult> SeedSceneRelationAsync(IRepository repository, IUnitOfWork unitOfWork, string suffix, bool includeSecondRelation = true)
    {
        var folder = await CreateFolderAsync(repository, unitOfWork, $"Folder-{suffix}");
        var scene = await CreateSceneAsync(repository, unitOfWork, folder.Id, $"Scene-{suffix}", "desc", KnowledgeSceneStatus.Published);
        var firstKnowledge = await CreateActiveKnowledgeAsync(repository, unitOfWork, $"First-{suffix}");
        var secondKnowledge = await CreateActiveKnowledgeAsync(repository, unitOfWork, $"Second-{suffix}");
        var now = DateTimeOffset.UtcNow;

        await repository.InsertAsync(new AiSpeechAssistantKnowledgeSceneRelation
        {
            KnowledgeId = firstKnowledge.Id,
            SceneId = scene.Id,
            CreatedAt = now.AddMinutes(-2)
        });

        if (includeSecondRelation)
        {
            await repository.InsertAsync(new AiSpeechAssistantKnowledgeSceneRelation
            {
                KnowledgeId = secondKnowledge.Id,
                SceneId = scene.Id,
                CreatedAt = now.AddMinutes(-1)
            });
        }

        await unitOfWork.SaveChangesAsync();

        return new RelationSaveSeedResult(scene.Id, firstKnowledge.Id, secondKnowledge.Id);
    }

    private sealed record FolderCascadeSeedResult(int FolderId, string FolderName, int SceneId, int SceneItemId, int RelationId, int KnowledgeId);

    private sealed record RelationSaveSeedResult(int SceneId, int FirstKnowledgeId, int SecondKnowledgeId);
}
