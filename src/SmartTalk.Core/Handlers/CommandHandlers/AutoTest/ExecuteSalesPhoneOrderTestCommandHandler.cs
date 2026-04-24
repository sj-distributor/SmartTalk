using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest.SalesAiOrder;
using SmartTalk.Messages.Commands.AutoTest.SalesPhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class ExecuteSalesPhoneOrderTestCommandHandler : ICommandHandler<ExecuteSalesPhoneOrderTestCommand>
{
    private readonly IAutoTestSalesPhoneOrderService _autoTestSalesPhoneOrderService;

    public ExecuteSalesPhoneOrderTestCommandHandler(IAutoTestSalesPhoneOrderService autoTestSalesPhoneOrderService)
    {
        _autoTestSalesPhoneOrderService = autoTestSalesPhoneOrderService;
    }

    public async Task Handle(IReceiveContext<ExecuteSalesPhoneOrderTestCommand> context, CancellationToken cancellationToken)
    {
        await _autoTestSalesPhoneOrderService.ExecuteSalesPhoneOrderTestAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}