using AutoMapper;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

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
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.AssistantName));
        
        CreateMap<AiSpeechAssistantSession, AiSpeechAssistantSessionDto>().ReverseMap();
    }
}