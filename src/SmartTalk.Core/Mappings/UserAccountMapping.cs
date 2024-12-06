using AutoMapper;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Messages.Dto.Account;

namespace SmartTalk.Core.Mappings;

public class UserAccountMapping : Profile
{
    public UserAccountMapping()
    {
        CreateMap<UserAccount, UserAccountDto>()
            .ForMember(x => x.LastModifiedByName, opt => opt.MapFrom(src => src.Creator));
        CreateMap<UserAccountDto, UserAccount>()
            .ForMember(x => x.Creator, opt => opt.MapFrom(src => src.LastModifiedByName));
        CreateMap<UserAccountProfile, UserAccountProfileDto>().ReverseMap();
    }
}