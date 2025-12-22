using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Printer;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Printer;

public class MerchPrinterOrderRetryCommand : ICommand
{
    public Guid? Id { get; set; }

    public int? OrderId { get; set; }

    public int? StoreId { get; set; }
}

public class MerchPrinterOrderRetryResponse : SmartTalkResponse<MerchPrinterOrderDto>
{
}