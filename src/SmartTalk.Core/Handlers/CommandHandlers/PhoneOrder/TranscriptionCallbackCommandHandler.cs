using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class TranscriptionCallbackCommandHandler : ICommandHandler<TranscriptionCallbackCommand, TranscriptionCallbackResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;
    
    public TranscriptionCallbackCommandHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }

    public async Task<TranscriptionCallbackResponse> Handle(IReceiveContext<TranscriptionCallbackCommand> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.TranscriptionCallbackAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
