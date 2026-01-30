using AutoMapper;
using SmartTalk.Core.Domain.Printer;
using SmartTalk.Message.Commands.Printer;
using SmartTalk.Messages.Commands.Printer;
using SmartTalk.Messages.Dto.Printer;
using SmartTalk.Messages.Enums.Printer;
using SmartTalk.Messages.Events.Printer;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Core.Mappings;

public class PrinterMapping : Profile
{
    public PrinterMapping()
    {
        CreateMap<MerchPrinterOrder, MerchPrinterOrderDto>();
        
        CreateMap<PrinterJobDto, PrinterJobCommand>()
            .ForMember(x => x.JobToken, dest => dest.MapFrom(y => y.Token))
            .ForMember(x => x.PrinterMac, dest => dest.MapFrom(y => y.Mac))
            ;
        
        CreateMap<ConfirmPrinterJobDto, ConfirmPrinterJobCommand>()
            .ForMember(x => x.JobToken, dest => dest.MapFrom(y => y.Token))
            .ForMember(x => x.PrinterMac, dest => dest.MapFrom(y => y.Mac))
            .ForMember(x => x.PrintStatusCode, dest => dest.MapFrom(y => y.Code))
            ;
        
        CreateMap<PrinterJobConfirmedEvent, MerchPrinterLog>()
            .ForMember(x => x.PrinterMac, dest => dest.MapFrom(y => y.PrinterMac))
            .ForMember(x => x.Code, dest => dest.MapFrom(y => y.GetCode()))
            .ForMember(x => x.CodeDescription, dest => dest.MapFrom(y => y.GetCodeDescription()))
            .ForMember(x => x.Id, dest => dest.MapFrom(y => Guid.NewGuid()))
            .ForMember(x => x.StoreId, dest => dest.MapFrom(y => y.MerchPrinterOrderDto.StoreId))
            .ForMember(x => x.OrderId, dest => dest.MapFrom(y => y.MerchPrinterOrderDto.OrderId))
            .ForMember(x => x.RecordId, dest => dest.MapFrom(y => y.MerchPrinterOrderDto.RecordId))
            .ForMember(x => x.PrintLogType, dest => dest.MapFrom(y => y.IsPrintError() ? PrintLogType.PrintError : PrintLogType.Print))
            ;
        
        CreateMap<MerchPrinter, MerchPrinterDto>()
            .ForMember(x => x.PrinterStatusInfo, dest => dest.MapFrom(y => y.PrinterStatusInfo()));
        
        CreateMap<AddMerchPrinterCommand, MerchPrinter>();
        
        CreateMap<UpdateMerchPrinterCommand, MerchPrinter>();
        
        CreateMap<MerchPrinterLog, MerchPrinterLogDto>()
            .ForMember(x => x.LogType, dest => dest.MapFrom(y => y.PrintLogType))
            .ForMember(x => x.Event, dest => dest.MapFrom(y => y.Message))
            .ForMember(x => x.Code, dest => dest.MapFrom(y => y.Code))
            .ForMember(x => x.CodeDescription, dest => dest.MapFrom(y => y.CodeDescription))
            .ForMember(x => x.Time, dest => dest.MapFrom(y => y.CreatedDate));
    }
}