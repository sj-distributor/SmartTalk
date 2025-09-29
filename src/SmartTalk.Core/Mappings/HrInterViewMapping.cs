using AutoMapper;
using SmartTalk.Core.Domain.HrInterView;
using SmartTalk.Messages.Dto.HrInterView;

namespace SmartTalk.Core.Mappings;

public class HrInterViewMapping : Profile
{
    public HrInterViewMapping()
    {
        CreateMap<HrInterViewSetting, HrInterViewSettingDto>().ReverseMap();
        CreateMap<HrInterViewSettingQuestion, HrInterViewSettingQuestionDto>().ReverseMap();
        CreateMap<HrInterViewSession, HrInterViewSessionDto>().ReverseMap();
    }
}