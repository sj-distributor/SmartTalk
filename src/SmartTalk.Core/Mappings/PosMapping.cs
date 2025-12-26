using AutoMapper;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Mappings;

public class PosMapping : Profile
{
    public PosMapping()
    {
        CreateMap<CompanyDto, Company>().ReverseMap();

        CreateMap<CompanyStoreDto, CompanyStore>().ReverseMap();
        CreateMap<CreateCompanyStoreCommand, CompanyStore>()
            .ForMember(dest => dest.PhoneNums, opt => opt.MapFrom(x => string.Join(",", x.PhoneNumbers)));
        CreateMap<UpdateCompanyStoreCommand, CompanyStore>()
            .ForMember(dest => dest.PhoneNums, opt => opt.MapFrom(x => string.Join(",", x.PhoneNumbers)));
        CreateMap<StoreUser, StoreUserDto>().ReverseMap();

        CreateMap<PosProductDto, PosProduct>().ReverseMap();
        CreateMap<PosCategoryDto, PosCategory>().ReverseMap();
        CreateMap<PosMenuDto, PosMenu>().ReverseMap();
        CreateMap<PosProduct, PosProductPayloadDto>();
        
        CreateMap<PosOrder, PosOrderDto>().ReverseMap();

        CreateMap<PlacePosOrderCommand, PosOrder>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(x => x.OrderItems));
        CreateMap<PosOrder, StoreCustomerDto>();

        CreateMap<StoreCustomer, StoreCustomerDto>().ReverseMap();
        CreateMap<UpdateStoreCustomerCommand, StoreCustomer>();

        CreateMap<PhoneOrderReservationInformation, OrderReservationInfoDto>().ReverseMap();
    }
}