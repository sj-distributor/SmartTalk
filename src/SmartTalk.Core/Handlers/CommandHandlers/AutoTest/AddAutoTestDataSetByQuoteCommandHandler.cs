using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class AddAutoTestDataSetByQuoteCommandHandler : ICommandHandler<AddAutoTestDataSetByQuoteCommand, AddAutoTestDataSetByQuoteResponse>
{
    private readonly IAutoTestService _autoTestService;

    public AddAutoTestDataSetByQuoteCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<AddAutoTestDataSetByQuoteResponse> Handle(IReceiveContext<AddAutoTestDataSetByQuoteCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.AddAutoTestDataSetByQuoteAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
