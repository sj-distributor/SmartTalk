using AutoMapper;
using SmartTalk.Core.Domain.KnowledgeScenario;
using SmartTalk.Messages.Commands.KnowledgeScenario;
using SmartTalk.Messages.Dto.KnowledgeScenario;

namespace SmartTalk.Core.Mappings;

public class KnowledgeScenarioMapping : Profile
{
    public KnowledgeScenarioMapping()
    {
        CreateMap<AddKnowledgeSceneCommand, KnowledgeScene>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description == null ? null : src.Description.Trim()));
        CreateMap<UpdateKnowledgeSceneCommand, KnowledgeScene>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description == null ? null : src.Description.Trim()))
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());
        CreateMap<AddKnowledgeSceneKnowledgeCommand, KnowledgeSceneKnowledge>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
            .ForMember(dest => dest.Content, opt => opt.MapFrom(src => src.Content == null ? null : src.Content.Trim()))
            .ForMember(dest => dest.FileName, opt => opt.MapFrom(src => src.FileName == null ? null : src.FileName.Trim()));
        CreateMap<UpdateKnowledgeSceneKnowledgeCommand, KnowledgeSceneKnowledge>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
            .ForMember(dest => dest.Content, opt => opt.MapFrom(src => src.Content == null ? null : src.Content.Trim()))
            .ForMember(dest => dest.FileName, opt => opt.MapFrom(src => src.FileName == null ? null : src.FileName.Trim()))
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.SceneId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());
        CreateMap<KnowledgeScene, KnowledgeSceneDto>();
        CreateMap<KnowledgeScene, KnowledgeSceneDetailDto>();
        CreateMap<KnowledgeSceneFolder, KnowledgeSceneFolderDto>();
        CreateMap<KnowledgeSceneKnowledge, KnowledgeSceneKnowledgeDto>();
    }
}
