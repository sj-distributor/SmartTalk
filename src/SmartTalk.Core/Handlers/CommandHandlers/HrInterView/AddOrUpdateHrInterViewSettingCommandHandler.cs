using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.HrInterView;
using SmartTalk.Messages.Commands.HrInterView;

namespace SmartTalk.Core.Handlers.CommandHandlers.HrInterView;

public class AddOrUpdateHrInterViewSettingCommandHandler : ICommandHandler<AddOrUpdateHrInterViewSettingCommand, AddOrUpdateHrInterViewSettingResponse>
{
    private readonly IHrInterViewService _hrInterViewService;

    public AddOrUpdateHrInterViewSettingCommandHandler(IHrInterViewService hrInterViewService)
    {
        _hrInterViewService = hrInterViewService;
    }

    public async Task<AddOrUpdateHrInterViewSettingResponse> Handle(IReceiveContext<AddOrUpdateHrInterViewSettingCommand> context, CancellationToken cancellationToken)
    {
        return await _hrInterViewService.AddOrUpdateHrInterViewSettingAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}