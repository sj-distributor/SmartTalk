using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Commands.SipServer;

namespace SmartTalk.Core.Handlers.CommandHandlers.SipServer;

public class UpdateDomainIpCommandHandler : ICommandHandler<UpdateDomainIpCommand, UpdateDomainIpResponse>
{
    private readonly IAlidnsClient _alidnsClient;
    
    public UpdateDomainIpCommandHandler(IAlidnsClient alidnsClient)
    {
        _alidnsClient = alidnsClient;
    }
    
    public async Task<UpdateDomainIpResponse> Handle(IReceiveContext<UpdateDomainIpCommand> context, CancellationToken cancellationToken)
    {
        return new UpdateDomainIpResponse
        {
            Data = await _alidnsClient.UpdateDomainRecordAsync(
                context.Message.DescribeDomain, context.Message.Endpoint, context.Message.HostRecords, context.Message.Value, cancellationToken).ConfigureAwait(false)
        };
    }
}