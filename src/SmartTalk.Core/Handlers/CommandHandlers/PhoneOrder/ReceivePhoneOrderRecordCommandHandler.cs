using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class ReceivePhoneOrderRecordCommandHandler : ICommandHandler<ReceivePhoneOrderRecordCommand>
{
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public ReceivePhoneOrderRecordCommandHandler(ISmartTalkHttpClientFactory httpClientFactory, ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _httpClientFactory = httpClientFactory;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task Handle(IReceiveContext<ReceivePhoneOrderRecordCommand> context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(context.Message.RecordUrl))
            context.Message.RecordContent = await _httpClientFactory.GetAsync<byte[]>(context.Message.RecordUrl, cancellationToken).ConfigureAwait(false);
        
        _backgroundJobClient.Enqueue<IPhoneOrderService>(x => x.ReceivePhoneOrderRecordAsync(context.Message, cancellationToken), HangfireConstants.InternalHostingPhoneOrder);
    }
}