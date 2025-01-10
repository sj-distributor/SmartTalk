using AutoMapper;
using SmartTalk.Core.Domain.PhoneCall;
using SmartTalk.Messages.Dto.PhoneOrder;

namespace SmartTalk.Core.Mappings;

public class PhoneOrderMapping : Profile
{
    public PhoneOrderMapping()
    {
        CreateMap<PhoneCallRecord, PhoneCallRecordDto>().ReverseMap();
        CreateMap<PhoneCallConversation, PhoneCallConversationDto>().ReverseMap();
        CreateMap<PhoneCallOrderItem, PhoneCallOrderItemDto>().ReverseMap();
    }
}