using AutoMapper;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;
using SmartTalk.Messages.Dto.KnowledgeScenario;

namespace SmartTalk.Core.Mappings;

public class KnowledgeScenarioMapping : Profile
{
    public KnowledgeScenarioMapping()
    {
        CreateMap<AddKnowledgeSceneCommand, KnowledgeScene>();
        CreateMap<UpdateKnowledgeSceneCommand, KnowledgeScene>();
        CreateMap<AddKnowledgeSceneItemDto, KnowledgeSceneItem>();
        CreateMap<UpdateKnowledgeSceneItemCommand, KnowledgeSceneItem>();
        CreateMap<KnowledgeScene, KnowledgeSceneDto>().ReverseMap();
        CreateMap<KnowledgeSceneFolder, KnowledgeSceneFolderDto>().ReverseMap();
        CreateMap<KnowledgeSceneItem, KnowledgeSceneItemDto>().ReverseMap();
    }
}
