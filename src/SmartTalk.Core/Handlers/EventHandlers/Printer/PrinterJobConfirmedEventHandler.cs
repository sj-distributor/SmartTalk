using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Events.Printer;

namespace SmartTalk.Core.Handlers.EventHandlers.Printer
{
    public class PrinterJobConfirmedEventHandler : IEventHandler<PrinterJobConfirmedEvent>
    {
        private readonly IPrinterService _printerService;

        public PrinterJobConfirmedEventHandler(IPrinterService printerService)
        {
            _printerService = printerService;
        }
        
        public async Task Handle(IReceiveContext<PrinterJobConfirmedEvent> context, CancellationToken cancellationToken)
        {
            await _printerService.PrinterJobConfirmeAsync(context.Message, cancellationToken).ConfigureAwait(false);
        }
    }
}