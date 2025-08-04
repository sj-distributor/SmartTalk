using AutoMapper;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Mappings;

public class AiSpeechAssistantMapping : Profile
{
    public AiSpeechAssistantMapping()
    {
        CreateMap<AiSpeechAssistant, AiSpeechAssistantDto>().ReverseMap();
        CreateMap<AddAiSpeechAssistantCommand, AiSpeechAssistant>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.AssistantName));

        CreateMap<AddAiSpeechAssistantKnowledgeCommand, AiSpeechAssistantKnowledge>();
        CreateMap<AiSpeechAssistantKnowledge, AiSpeechAssistantKnowledgeDto>().ReverseMap();
        CreateMap<NumberPool, NumberPoolDto>().ReverseMap();

        CreateMap<UpdateAiSpeechAssistantCommand, AiSpeechAssistant>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.AssistantName))
            .ForMember(dest => dest.ModelVoice, opt => opt.MapFrom(src => src.Voice));
        
        CreateMap<AiSpeechAssistantSession, AiSpeechAssistantSessionDto>().ReverseMap();
        CreateMap<ExtractedOrderItemDto, AiOrderItemDto>()
            .ForMember(dest => dest.AiMaterialDesc, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.MateialQuantity, opt => opt.MapFrom(src => src.Quantity))
            .ForMember(dest => dest.MaterialNumber, opt => opt.MapFrom(src => src.MaterialNumber));
    }
}