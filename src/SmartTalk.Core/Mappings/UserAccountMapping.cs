using AutoMapper;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Messages.Dto.Account;
using UserAccountDto = SmartTalk.Messages.Dto.Users.UserAccountDto;

namespace SmartTalk.Core.Mappings;

public class UserAccountMapping : Profile
{
    public UserAccountMapping()
    {
        CreateMap<UserAccount, UserAccountDto>().ReverseMap();
        CreateMap<UserAccountProfile, UserAccountProfileDto>().ReverseMap();
    }
}