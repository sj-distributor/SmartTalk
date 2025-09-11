using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Commands.Printer;

namespace SmartTalk.Core.Handlers.CommandHandlers.Printer
{
    public class PrintTestCommandHandler : ICommandHandler<PrintTestCommand, PrintTestResponse>
    {
        private readonly IPrinterService _printerService;

        public PrintTestCommandHandler(IPrinterService printerService)
        {
            _printerService = printerService;
        }
        
        public async Task<PrintTestResponse> Handle(IReceiveContext<PrintTestCommand> context, CancellationToken cancellationToken)
        {
            return await _printerService.PrintTestAsync(context.Message, cancellationToken).ConfigureAwait(false);
        }
    }
}