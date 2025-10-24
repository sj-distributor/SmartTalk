using AutoMapper;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Messages.Dto.AutoTest;

namespace SmartTalk.Core.Mappings;

public class AutoTestMapping : Profile
{
    public AutoTestMapping()
    {
        CreateMap<AutoTestDataItem, AutoTestDataItemDto>().ReverseMap();
        CreateMap<AutoTestDataSet, AutoTestDataSetDto>().ReverseMap();
        CreateMap<AutoTestDataSetItem, AutoTestDataSetItemDto>().ReverseMap();
        CreateMap<AutoTestImportDataRecord, AutoTestImportDataRecordDto>().ReverseMap();
        CreateMap<AutoTestScenario, AutoTestScenarioDto>().ReverseMap();
        CreateMap<AutoTestTask, AutoTestTaskDto>().ReverseMap();
        CreateMap<AutoTestTaskRecord, AutoTestTaskRecordDto>().ReverseMap();
    }
}