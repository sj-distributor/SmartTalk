using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class UpdatePhoneOrderRecordTasksCommandHandler : ICommandHandler<UpdatePhoneOrderRecordTasksCommand, UpdatePhoneOrderRecordTasksResponse>
{
    private readonly IPhoneOrderService _phoneOrderService;
    
    public UpdatePhoneOrderRecordTasksCommandHandler(IPhoneOrderService phoneOrderService)
    {
        _phoneOrderService = phoneOrderService;
    }
    
    public async Task<UpdatePhoneOrderRecordTasksResponse> Handle(IReceiveContext<UpdatePhoneOrderRecordTasksCommand> context, CancellationToken cancellationToken)
    {
        return await _phoneOrderService.UpdatePhoneOrderRecordTasksAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}