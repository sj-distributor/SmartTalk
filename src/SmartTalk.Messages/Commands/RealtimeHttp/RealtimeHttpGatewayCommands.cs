using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.RealtimeHttp;

namespace SmartTalk.Messages.Commands.RealtimeHttp;

public class CreateRealtimeHttpSessionCommand : RealtimeHttpCreateSessionRequest, ICommand
{
}

public class SendRealtimeHttpSessionMessageCommand : RealtimeHttpSendMessageRequest, ICommand
{
    public string SessionId { get; set; } = string.Empty;
}

public class DisconnectRealtimeHttpSessionCommand : ICommand
{
    public string SessionId { get; set; } = string.Empty;

    public string Reason { get; set; } = "http_client_disconnect";
}
