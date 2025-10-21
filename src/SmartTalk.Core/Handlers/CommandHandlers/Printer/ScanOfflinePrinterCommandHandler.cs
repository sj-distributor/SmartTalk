using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Commands.Printer;

namespace SmartTalk.Core.Handlers.CommandHandlers.Printer;

public class ScanOfflinePrinterCommandHandler : ICommandHandler<ScanOfflinePrinterCommand>
{
    private readonly IPrinterService _printerService;

    public ScanOfflinePrinterCommandHandler(IPrinterService printerService)
    {
        _printerService = printerService;
    }
    
    public async Task Handle(IReceiveContext<ScanOfflinePrinterCommand> context, CancellationToken cancellationToken)
    {
        await _printerService.ScanOfflinePrinter(cancellationToken).ConfigureAwait(false);
    }
}