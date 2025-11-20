using AutoMapper;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Dto.Sales;
using SmartTalk.Messages.Requests.AutoTest;

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
        

        CreateMap<ExtractedOrderItemDto, AutoTestInputDetail>()
            .ForMember(dest => dest.ItemName, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
            .ForMember(dest => dest.ItemId, opt => opt.MapFrom(src => src.MaterialNumber));
        
        CreateMap<AutoTestDataSetDataRecords,AutoTestDataSet>().ReverseMap();
    }
}