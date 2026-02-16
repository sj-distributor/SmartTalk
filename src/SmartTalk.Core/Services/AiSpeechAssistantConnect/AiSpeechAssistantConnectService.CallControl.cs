using System.Net.WebSockets;
using System.Text.Json;
using Mediator.Net;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task HandleForwardOnlyAsync(string forwardPhoneNumber, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 10];

        try
        {
            while (_ctx.TwilioWebSocket.State == WebSocketState.Open)
            {
                var result = await _ctx.TwilioWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.Count == 0) continue;

                using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));
                var eventMessage = jsonDocument?.RootElement.GetProperty("event").GetString();

                switch (eventMessage)
                {
                    case "start":
                        HandleForwardStart(jsonDocument, forwardPhoneNumber);
                        break;
                    case "stop":
                        HandleForwardStop();
                        break;
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error(ex, "[AiAssistant] Forward-only WebSocket error, CallSid: {CallSid}", _ctx.CallSid);
        }
    }

    private void HandleForwardStart(JsonDocument jsonDocument, string forwardPhoneNumber)
    {
        _ctx.CallSid = jsonDocument.RootElement.GetProperty("start").GetProperty("callSid").GetString();
        _ctx.StreamSid = jsonDocument.RootElement.GetProperty("start").GetProperty("streamSid").GetString();

        Log.Information("[AiAssistant] Forward-only started, CallSid: {CallSid}, ForwardNumber: {ForwardNumber}",
            _ctx.CallSid, forwardPhoneNumber);

        _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new RecordAiSpeechAssistantCallCommand
        {
            CallSid = _ctx.CallSid, Host = _ctx.Host
        }, CancellationToken.None), HangfireConstants.InternalHostingRecordPhoneCall);

        _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
        {
            CallSid = _ctx.CallSid,
            HumanPhone = forwardPhoneNumber
        }, CancellationToken.None));
    }

    private void HandleForwardStop()
    {
        _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x =>
            x.RecordAiSpeechAssistantCallAsync(new AiSpeechAssistantStreamContextDto
            {
                CallSid = _ctx.CallSid,
                StreamSid = _ctx.StreamSid,
                Host = _ctx.Host
            }, _ctx.OrderRecordType, CancellationToken.None));
    }
}
