using AutoMapper;
using SmartTalk.Core.Domain.Asterisk;
using SmartTalk.Messages.Requests.Twilio;

namespace SmartTalk.Core.Mappings;

public class TwilioMapping : Profile
{
    public TwilioMapping()
    {
        CreateMap<AsteriskCdr, GetAsteriskCdrData>().ReverseMap();
    }
}