using AutoMapper;
using SmartTalk.Core.Domain.Asterisk;
using SmartTalk.Core.Domain.Linphone;
using SmartTalk.Messages.Dto.Linphone;

namespace SmartTalk.Core.Mappings;

public class LinphoneMapping : Profile
{
    public LinphoneMapping()
    {
        CreateMap<LinphoneCdr, LinphoneHistoryDto>().ReverseMap();
        
        CreateMap<LinphoneCdr, LinphoneCdrDto>().ReverseMap();

        CreateMap<LinphoneCdrDto, Cdr>();
    }
}