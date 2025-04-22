using AutoMapper;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Core.Mappings;

public class VoiceAiMapping : Profile
{
    public VoiceAiMapping()
    {
        CreateMap<PosCompanyStoreDto, PosCompanyStore>().ReverseMap();
    }
}