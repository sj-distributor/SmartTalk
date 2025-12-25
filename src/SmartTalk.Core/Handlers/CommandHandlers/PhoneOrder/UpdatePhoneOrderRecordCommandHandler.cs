using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class UpdatePhoneOrderRecordCommandHandler : ICommandHandler<UpdatePhoneOrderRecordCommand, UpdatePhoneOrderRecordResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;
    
    public UpdatePhoneOrderRecordCommandHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }
    
    public async Task<UpdatePhoneOrderRecordResponse> Handle(IReceiveContext<UpdatePhoneOrderRecordCommand> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.UpdatePhoneOrderRecordAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}