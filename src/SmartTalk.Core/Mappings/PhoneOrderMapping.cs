using AutoMapper;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;

namespace SmartTalk.Core.Mappings;

public class PhoneOrderMapping : Profile
{
    public PhoneOrderMapping()
    {
        CreateMap<PhoneOrderRecord, PhoneOrderRecordDto>().ReverseMap();
        CreateMap<PhoneOrderConversation, PhoneOrderConversationDto>().ReverseMap();
        CreateMap<PhoneOrderOrderItem, PhoneOrderOrderItemDto>().ReverseMap();
    }
}