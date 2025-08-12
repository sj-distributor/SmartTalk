using AutoMapper;
using SmartTalk.Core.Domain.Printer;
using SmartTalk.Messages.Commands.Printer;
using SmartTalk.Messages.Dto.Printer;

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
    }
}