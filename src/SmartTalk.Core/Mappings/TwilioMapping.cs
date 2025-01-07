using AutoMapper;
using SmartTalk.Core.Domain.Asterisk;
using SmartTalk.Messages.Requests.Twilio;

namespace SmartTalk.Core.Mappings;

public class TwilioMapping : Profile
{
    public TwilioMapping()
    {
        CreateMap<GetAsteriskCdrData, AsteriskCdr>()
            .ForMember(des => des.CreatedDate, opt => opt.MapFrom(x => x.CallDate));
    }
}