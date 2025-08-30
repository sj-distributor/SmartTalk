using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Printer;

public class DeleteMerchPrinterCommand : ICommand
{
    public int Id { get; set; }
}

public class DeleteMerchPrinterResponse : SmartTalkResponse
{
}