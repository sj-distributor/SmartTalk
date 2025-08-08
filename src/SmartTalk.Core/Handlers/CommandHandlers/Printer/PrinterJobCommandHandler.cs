using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Commands.Printer;

namespace SmartTalk.Core.Handlers.CommandHandlers.Printer;

public class PrinterJobCommandHandler : ICommandHandler<PrinterJobCommand, PrinterJobResponse>
{
    private readonly IPrinterService _printerService;

    public PrinterJobCommandHandler(IPrinterService printerService)
    {
        _printerService = printerService;
    }

    public async Task<PrinterJobResponse> Handle(IReceiveContext<PrinterJobCommand> context, CancellationToken cancellationToken)
    {
        return await _printerService.PrinterJob(context.Message, cancellationToken).ConfigureAwait(false);
    }
}