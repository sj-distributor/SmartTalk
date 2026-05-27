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
        CreateMap<KnowledgeSceneItemDto, KnowledgeSceneItem>().ReverseMap();
        CreateMap<KnowledgeScene, KnowledgeSceneDto>().ReverseMap();
        CreateMap<KnowledgeSceneHistory, KnowledgeSceneHistoryDto>();
        CreateMap<KnowledgeSceneFolder, KnowledgeSceneFolderDto>().ReverseMap();
        CreateMap<KnowledgeSceneCompany, KnowledgeSceneCompanyDto>().ReverseMap();
    }
}
