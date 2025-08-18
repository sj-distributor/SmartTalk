using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Message.Events.Printer;

namespace SmartTalk.Core.Handlers.EventHandlers.Printer
{
    public class PrinterStatusChangedEventHandler : IEventHandler<PrinterStatusChangedEvent>
    {
        private readonly IPrinterService _printerService;

        public PrinterStatusChangedEventHandler(IPrinterService printerService)
        {
            _printerService = printerService;
        }

        public async Task Handle(IReceiveContext<PrinterStatusChangedEvent> context,
            CancellationToken cancellationToken)
        {
            if (context.Message.Skip())
                return;

            await _printerService.PrinterStatusChangedAsync(context.Message, cancellationToken).ConfigureAwait(false);
        }
    }
}