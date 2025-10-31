using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class CopyAutoTestDataItemsCommandHandler : ICommandHandler<CopyAutoTestDataSetCommand, CopyAutoTestDataSetResponse>
{
    private readonly IAutoTestService _autoTestService;

    public CopyAutoTestDataItemsCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<CopyAutoTestDataSetResponse> Handle(IReceiveContext<CopyAutoTestDataSetCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.CopyAutoTestDataItemsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}