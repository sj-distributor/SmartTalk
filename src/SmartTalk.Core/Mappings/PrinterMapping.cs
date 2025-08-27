using AutoMapper;
using SmartTalk.Core.Domain.Printer;
using SmartTalk.Messages.Commands.Printer;
using SmartTalk.Messages.Dto.Printer;
using SmartTalk.Messages.Enums.Printer;
using SmartTalk.Messages.Events.Printer;

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
            .ForMember(x => x.AgentId, dest => dest.MapFrom(y => y.MerchPrinterOrderDto.AgentId))
            .ForMember(x => x.OrderId, dest => dest.MapFrom(y => y.MerchPrinterOrderDto.OrderId))
            .ForMember(x => x.PrintLogType, dest => dest.MapFrom(y => y.IsPrintError() ? PrintLogType.PrintError : PrintLogType.Print))
            ;
    }
}