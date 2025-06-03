using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Messages.Commands.Linphone;

namespace SmartTalk.Core.Handlers.CommandHandlers.Linphone;

public class AutoGetLinphoneCdrRecordCommandHandler : ICommandHandler<SchedulingAutoGetLinphoneCdrRecordCommand>
{
    private readonly ILinphoneService _linphoneService;

    public AutoGetLinphoneCdrRecordCommandHandler(ILinphoneService linphoneService)
    {
        _linphoneService = linphoneService;
    }

    public async Task Handle(IReceiveContext<SchedulingAutoGetLinphoneCdrRecordCommand> context, CancellationToken cancellationToken)
    {
        await _linphoneService.AutoGetLinphoneCdrRecordAsync(cancellationToken).ConfigureAwait(false);
    }
}