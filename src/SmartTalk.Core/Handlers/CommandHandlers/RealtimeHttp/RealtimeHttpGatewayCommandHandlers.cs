using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.RealtimeHttp;
using SmartTalk.Messages.Commands.RealtimeHttp;
using SmartTalk.Messages.Dto.RealtimeHttp;

namespace SmartTalk.Core.Handlers.CommandHandlers.RealtimeHttp;

public class CreateRealtimeHttpSessionCommandHandler : ICommandHandler<CreateRealtimeHttpSessionCommand, RealtimeHttpCreateSessionResponse>
{
    private readonly IRealtimeHttpGatewayService _service;

    public CreateRealtimeHttpSessionCommandHandler(IRealtimeHttpGatewayService service)
    {
        _service = service;
    }

    public async Task<RealtimeHttpCreateSessionResponse> Handle(IReceiveContext<CreateRealtimeHttpSessionCommand> context, CancellationToken cancellationToken)
    {
        return await _service.CreateSessionAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}

public class RunDefaultRealtimeHttpConversationHandler : ICommandHandler<RealtimeHttpRunDefaultConversationRequest, RealtimeHttpRunDefaultConversationResponse>
{
    private readonly IRealtimeHttpGatewayService _service;

    public RunDefaultRealtimeHttpConversationHandler(IRealtimeHttpGatewayService service)
    {
        _service = service;
    }

    public async Task<RealtimeHttpRunDefaultConversationResponse> Handle(IReceiveContext<RealtimeHttpRunDefaultConversationRequest> context, CancellationToken cancellationToken)
    {
        return await _service.RunDefaultConversationAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}

public class SendRealtimeHttpSessionMessageCommandHandler : ICommandHandler<SendRealtimeHttpSessionMessageCommand, RealtimeHttpSendMessageResponse>
{
    private readonly IRealtimeHttpGatewayService _service;

    public SendRealtimeHttpSessionMessageCommandHandler(IRealtimeHttpGatewayService service)
    {
        _service = service;
    }

    public async Task<RealtimeHttpSendMessageResponse> Handle(IReceiveContext<SendRealtimeHttpSessionMessageCommand> context, CancellationToken cancellationToken)
    {
        var request = new RealtimeHttpSendMessageRequest
        {
            Text = context.Message.Text,
            TimeoutMs = context.Message.TimeoutMs
        };

        return await _service.SendMessageAsync(context.Message.SessionId, request, cancellationToken).ConfigureAwait(false);
    }
}

public class DisconnectRealtimeHttpSessionCommandHandler : ICommandHandler<DisconnectRealtimeHttpSessionCommand, RealtimeHttpDisconnectResponse>
{
    private readonly IRealtimeHttpGatewayService _service;

    public DisconnectRealtimeHttpSessionCommandHandler(IRealtimeHttpGatewayService service)
    {
        _service = service;
    }

    public async Task<RealtimeHttpDisconnectResponse> Handle(IReceiveContext<DisconnectRealtimeHttpSessionCommand> context, CancellationToken cancellationToken)
    {
        return await _service.DisconnectSessionAsync(context.Message.SessionId, context.Message.Reason, cancellationToken).ConfigureAwait(false);
    }
}
