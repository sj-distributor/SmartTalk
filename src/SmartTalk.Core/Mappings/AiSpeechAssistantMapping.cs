using AutoMapper;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Core.Mappings;

public class AiSpeechAssistantMapping : Profile
{
    public AiSpeechAssistantMapping()
    {
        CreateMap<AiSpeechAssistant, AiSpeechAssistantDto>().ReverseMap();
    }
}