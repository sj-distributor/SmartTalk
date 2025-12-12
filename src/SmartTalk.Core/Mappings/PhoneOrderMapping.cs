using AutoMapper;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.WebSocket;

namespace SmartTalk.Core.Mappings;

public class PhoneOrderMapping : Profile
{
    public PhoneOrderMapping()
    {
        CreateMap<PhoneOrderRecord, PhoneOrderRecordDto>()
            .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => TimeZoneInfo.ConvertTimeFromUtc(src.CreatedDate.UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"))));
        
        CreateMap<PhoneOrderRecordDto, PhoneOrderRecord>();
        CreateMap<PhoneOrderConversation, PhoneOrderConversationDto>().ReverseMap();
        CreateMap<PhoneOrderOrderItem, PhoneOrderOrderItemDto>().ReverseMap();
        
        CreateMap<AiSpeechAssistantOrderDto, PhoneOrderDetailDto>()
            .ForMember(dest => dest.FoodDetails, opt => opt.MapFrom(src => src.Order));

        CreateMap<AiSpeechAssistantOrderItemDto, FoodDetailDto>()
            .ForMember(dest => dest.FoodName, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Quantity.ToString()))
            .ForMember(dest => dest.Remark, opt => opt.MapFrom(src => src.Comments))
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => (double)src.Price))
            .ForMember(dest => dest.ProductId, opt => opt.Ignore());
        
        CreateMap<EasyPosResponseLocalization, PhoneCallOrderItemLocalization>().ReverseMap();
        CreateMap<EasyPosResponseLocalization, PhoneCallOrderItemModifierLocalization>().ReverseMap();
        CreateMap<PhoneCallOrderItemModifiers, EasyPosOrderItemModifiersDto>().ReverseMap();
        CreateMap<EasyPosLocalizationsDto, PhoneCallOrderItemLocalization>().ReverseMap();
        CreateMap<EasyPosLocalizationsDto, PhoneCallOrderItemModifierLocalization>().ReverseMap();
        
        CreateMap<PhoneOrderRecordReportDto, PhoneOrderRecordReport>().ReverseMap();
        
        CreateMap<PhoneOrderRecordScenarioHistory, PhoneOrderRecordScenarioHistoryDto>().ReverseMap();
    }
}