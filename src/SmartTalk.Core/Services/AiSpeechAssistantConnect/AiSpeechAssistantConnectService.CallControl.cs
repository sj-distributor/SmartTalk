using System.Net.WebSockets;
using System.Text.Json;
using Mediator.Net;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task HandleForwardOnlyAsync(
        WebSocket twilioWebSocket, SessionBusinessContext ctx, PhoneOrderRecordType orderRecordType, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 10];

        try
        {
            while (twilioWebSocket.State == WebSocketState.Open)
            {
                var result = await twilioWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close) break;

                if (result.Count > 0)
                {
                    using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));
                    var eventMessage = jsonDocument?.RootElement.GetProperty("event").GetString();

                    switch (eventMessage)
                    {
                        case "start":
                            ctx.CallSid = jsonDocument.RootElement.GetProperty("start").GetProperty("callSid").GetString();
                            ctx.StreamSid = jsonDocument.RootElement.GetProperty("start").GetProperty("streamSid").GetString();

                            Log.Information("[AiSpeechAssistantConnect] Forward-only: captured CallSid={CallSid}, forwarding to {ForwardNumber}",
                                ctx.CallSid, ctx.ForwardPhoneNumber);

                            _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new RecordAiSpeechAssistantCallCommand
                            {
                                CallSid = ctx.CallSid, Host = ctx.Host
                            }, CancellationToken.None), HangfireConstants.InternalHostingRecordPhoneCall);

                            _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
                            {
                                CallSid = ctx.CallSid,
                                HumanPhone = ctx.ForwardPhoneNumber
                            }, CancellationToken.None));
                            break;

                        case "stop":
                            _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x =>
                                x.RecordAiSpeechAssistantCallAsync(new AiSpeechAssistantStreamContextDto
                                {
                                    CallSid = ctx.CallSid,
                                    StreamSid = ctx.StreamSid,
                                    Host = ctx.Host
                                }, orderRecordType, CancellationToken.None));
                            break;
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error("Forward-only WebSocket error: {@ex}", ex);
        }
    }
}
