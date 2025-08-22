using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Message.Commands.Printer;

namespace SmartTalk.Core.Handlers.CommandHandlers.Printer
{
    public class AddMerchPrinterCommandHandler : ICommandHandler<AddMerchPrinterCommand>
    {
        private readonly IPrinterService _printerService;

        public AddMerchPrinterCommandHandler(IPrinterService printerService)
        {
            _printerService = printerService;
        }

        public async Task Handle(IReceiveContext<AddMerchPrinterCommand> context, CancellationToken cancellationToken)
        {
            await _printerService.AddMerchPrinterAsync(context.Message, cancellationToken).ConfigureAwait(false);
        }
    }
}