using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Commands.Printer;

namespace SmartTalk.Core.Handlers.CommandHandlers.Printer;

public class MerchPrinterOrderRetryCommandHandler :  ICommandHandler<MerchPrinterOrderRetryCommand, MerchPrinterOrderRetryResponse>
{
    private readonly IPrinterService _printerService;

    public MerchPrinterOrderRetryCommandHandler(IPrinterService printerService)
    {
        _printerService = printerService;
    }
    
    public async Task<MerchPrinterOrderRetryResponse> Handle(IReceiveContext<MerchPrinterOrderRetryCommand> context, CancellationToken cancellationToken)
    {
        return await _printerService.MerchPrinterOrderRetryAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}