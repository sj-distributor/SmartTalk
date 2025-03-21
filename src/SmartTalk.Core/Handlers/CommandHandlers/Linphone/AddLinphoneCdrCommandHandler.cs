using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Messages.Commands.Linphone;

namespace SmartTalk.Core.Handlers.CommandHandlers.Linphone;

public class AddLinphoneCdrCommandHandler : ICommandHandler<AddLinphoneCdrCommand>
{
    private readonly ILinphoneService _linphoneService;
    
    public AddLinphoneCdrCommandHandler(ILinphoneService linphoneService)
    {
        _linphoneService = linphoneService;
    }
     
    public async Task Handle(IReceiveContext<AddLinphoneCdrCommand> context, CancellationToken cancellationToken)
    {
        await _linphoneService.AddLinphoneCdrAsync(context.Message.RecordName, cancellationToken).ConfigureAwait(false);
    }
}