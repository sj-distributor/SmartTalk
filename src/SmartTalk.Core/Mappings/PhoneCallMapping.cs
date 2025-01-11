using AutoMapper;
using SmartTalk.Core.Domain.PhoneCall;
using SmartTalk.Messages.Dto.PhoneCall;

namespace SmartTalk.Core.Mappings;

public class PhoneCallMapping : Profile
{
    public PhoneCallMapping()
    {
        CreateMap<PhoneCallRecord, PhoneCallRecordDto>().ReverseMap();
        CreateMap<PhoneCallConversation, PhoneCallConversationDto>().ReverseMap();
        CreateMap<PhoneCallOrderItem, PhoneCallOrderItemDto>().ReverseMap();
    }
}