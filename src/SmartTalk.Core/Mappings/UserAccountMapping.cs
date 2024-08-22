using AutoMapper;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Messages.Dto.Account;

namespace SmartTalk.Core.Mappings;

public class UserAccountMapping : Profile
{
    public UserAccountMapping()
    {
        CreateMap<UserAccount, UserAccountDto>().ReverseMap();
        CreateMap<UserAccountProfile, UserAccountProfileDto>().ReverseMap();
    }
}