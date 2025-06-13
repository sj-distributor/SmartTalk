using AutoMapper;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Core.Mappings;

public class PosMapping : Profile
{
    public PosMapping()
    {
        CreateMap<PosCompanyDto, PosCompany>().ReverseMap();

        CreateMap<PosCompanyStoreDto, PosCompanyStore>().ReverseMap();
        CreateMap<CreatePosCompanyStoreCommand, PosCompanyStore>()
            .ForMember(dest => dest.PhoneNums, opt => opt.MapFrom(x => string.Join(",", x.PhoneNumbers)));
        CreateMap<UpdatePosCompanyStoreCommand, PosCompanyStore>()
            .ForMember(dest => dest.PhoneNums, opt => opt.MapFrom(x => string.Join(",", x.PhoneNumbers)));
        CreateMap<PosStoreUser, PosStoreUserDto>().ReverseMap();

        CreateMap<PosProductDto, PosProduct>().ReverseMap();
        CreateMap<PosCategoryDto, PosCategory>().ReverseMap();
        CreateMap<PosMenuDto, PosMenu>().ReverseMap();
        CreateMap<PosProduct, PosProductPayloadDto>();
        
        CreateMap<PosOrder, PosOrderDto>().ReverseMap();

        CreateMap<PlacePosOrderCommand, PosOrder>();
    }
}