using AutoMapper;
using SmartTalk.Core.Domain.Printer;
using SmartTalk.Messages.Dto.Printer;

namespace SmartTalk.Core.Mappings;

public class PrinterMapping : Profile
{
    public PrinterMapping()
    {
        CreateMap<MerchPrinterOrder, MerchPrinterOrderDto>();
    }
}