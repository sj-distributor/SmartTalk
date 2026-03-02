using AutoMapper;
using SmartTalk.Core.Domain.Hr;
using SmartTalk.Messages.Dto.Hr;

namespace SmartTalk.Core.Mappings;

public class HrMapping : Profile
{
    public HrMapping()
    {
        CreateMap<HrInterviewQuestion, HrInterviewQuestionDto>().ReverseMap();
    }
}