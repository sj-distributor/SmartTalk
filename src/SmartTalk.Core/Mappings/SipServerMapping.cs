using AutoMapper;
using SmartTalk.Core.Domain.SipServer;
using SmartTalk.Messages.Dto.SipServer;

namespace SmartTalk.Core.Mappings;

public class SipServerMapping : Profile
{
    public SipServerMapping()
    {
        CreateMap<SipHostServer, SipHostServerDto>().ReverseMap();
        CreateMap<SipBackupServer, SipBackupServerDto>().ReverseMap();
    }
}