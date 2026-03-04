using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class AutoTestImportCommandHandler : ICommandHandler<AutoTestImportDataCommand, AutoTestImportDataResponse>
{
    private readonly IAutoTestService _autoTestService;

    public AutoTestImportCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<AutoTestImportDataResponse> Handle(IReceiveContext<AutoTestImportDataCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.AutoTestImportDataAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
