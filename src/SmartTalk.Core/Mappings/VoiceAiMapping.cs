using AutoMapper;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Core.Mappings;

public class VoiceAiMapping : Profile
{
    public VoiceAiMapping()
    {
        CreateMap<PosCompanyDto, PosCompany>().ReverseMap();

        CreateMap<PosCompanyStoreDto, PosCompanyStore>().ReverseMap();
        CreateMap<CreatePosCompanyStoreCommand, PosCompanyStoreDto>()
            .ForMember(dest => dest.PhoneNums, opt => opt.MapFrom(x => string.Join(",", x.PhoneNumbers)));
        CreateMap<UpdatePosCompanyStoreCommand, PosCompanyStoreDto>()
            .ForMember(dest => dest.PhoneNums, opt => opt.MapFrom(x => string.Join(",", x.PhoneNumbers)));
        CreateMap<PosStoreUser, PosStoreUserDto>().ReverseMap();

        CreateMap<PosProductDto, PosProduct>().ReverseMap();
        CreateMap<PosCategoryDto, PosCategory>().ReverseMap();
        CreateMap<PosMenuDto, PosMenu>().ReverseMap();
    }
}