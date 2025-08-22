using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Messages.Commands.Printer;

namespace SmartTalk.Core.Handlers.CommandHandlers.Printer;

public class UpdateMerchPrinterCommandHandler : ICommandHandler<UpdateMerchPrinterCommand>
{
    private readonly IPrinterService _printerService;

    public UpdateMerchPrinterCommandHandler(IPrinterService printerService)
    {
        _printerService = printerService;
    }
    
    public async Task Handle(IReceiveContext<UpdateMerchPrinterCommand> context, CancellationToken cancellationToken)
    {
        await _printerService.UpdateMerchPrinterAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}