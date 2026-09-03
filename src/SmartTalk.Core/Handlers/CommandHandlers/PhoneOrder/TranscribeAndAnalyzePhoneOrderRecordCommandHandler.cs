using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Handlers.CommandHandlers.PhoneOrder;

public class TranscribeAndAnalyzePhoneOrderRecordCommandHandler : ICommandHandler<TranscribeAndAnalyzePhoneOrderRecordCommand, TranscribeAndAnalyzePhoneOrderRecordResponse>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public TranscribeAndAnalyzePhoneOrderRecordCommandHandler(ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public Task<TranscribeAndAnalyzePhoneOrderRecordResponse> Handle(IReceiveContext<TranscribeAndAnalyzePhoneOrderRecordCommand> context, CancellationToken cancellationToken)
    {
        var command = context.Message ?? throw new ArgumentNullException(nameof(context.Message));

        if (string.IsNullOrWhiteSpace(command.RecordingUrl))
            throw new InvalidOperationException("RecordingUrl is required.");

        if (command.AgentId <= 0)
            throw new InvalidOperationException("AgentId is required.");

        var receiveCommand = new ReceiveAixvolinkPhoneOrderRecordCommand
        {
            RecordingUrl = command.RecordingUrl.Trim(),
            CallTime = command.CallTime ?? DateTimeOffset.UtcNow,
            CallerNumber = command.CallerNumber,
            CalleeNumber = command.CalleeNumber,
            AgentId = command.AgentId,
            AssistantId = command.AssistantId,
            OrderRecordType = command.OrderRecordType
        };
        
        _backgroundJobClient.Enqueue<IPhoneOrderService>(
            x => x.ReceiveAixvolinkPhoneOrderRecordAsync(receiveCommand, CancellationToken.None),
            HangfireConstants.InternalHostingAixvolinkPhoneOrder);

        return Task.FromResult(new TranscribeAndAnalyzePhoneOrderRecordResponse
        {
            Data = new TranscribeAndAnalyzePhoneOrderRecordResponseData
            {
                RecordingUrl = receiveCommand.RecordingUrl,
                Status = "Queued"
            }
        });
    }
}
