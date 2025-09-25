using AutoMapper;
using SmartTalk.Core.Domain.HrInterView;
using SmartTalk.Messages.Dto.HrInterView;

namespace SmartTalk.Core.Mappings;

public class HrInterViewMapping : Profile
{
    public HrInterViewMapping()
    {
        CreateMap<HrInterViewSetting, HrInterViewSettingDto>();
        CreateMap<HrInterViewSettingQuestion, HrInterViewSettingQuestionDto>();
        CreateMap<HrInterViewSession, HrInterViewSessionDto>();
    }
}