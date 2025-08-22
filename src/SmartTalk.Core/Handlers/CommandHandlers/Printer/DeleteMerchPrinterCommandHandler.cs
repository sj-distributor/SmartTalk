using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Commands.Printer;

namespace SmartTalk.Core.Handlers.CommandHandlers.Printer;

public class DeleteMerchPrinterCommandHandler : ICommandHandler<DeleteMerchPrinterCommand>
{
    private readonly IPrinterService _printerService;

    public DeleteMerchPrinterCommandHandler(IPrinterService printerService)
    {
        _printerService = printerService;
    }

    public async Task Handle(IReceiveContext<DeleteMerchPrinterCommand> context, CancellationToken cancellationToken)
    {
        await _printerService.DeleteMerchPrinterAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}