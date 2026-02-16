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
        WebSocket twilioWebSocket, string host, string forwardPhoneNumber,
        PhoneOrderRecordType orderRecordType, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 10];
        string callSid = null;
        string streamSid = null;

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
                            callSid = jsonDocument.RootElement.GetProperty("start").GetProperty("callSid").GetString();
                            streamSid = jsonDocument.RootElement.GetProperty("start").GetProperty("streamSid").GetString();

                            Log.Information("[AiAssistant] Forward-only started, CallSid: {CallSid}, ForwardNumber: {ForwardNumber}",
                                callSid, forwardPhoneNumber);

                            _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new RecordAiSpeechAssistantCallCommand
                            {
                                CallSid = callSid, Host = host
                            }, CancellationToken.None), HangfireConstants.InternalHostingRecordPhoneCall);

                            _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
                            {
                                CallSid = callSid,
                                HumanPhone = forwardPhoneNumber
                            }, CancellationToken.None));
                            break;

                        case "stop":
                            _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x =>
                                x.RecordAiSpeechAssistantCallAsync(new AiSpeechAssistantStreamContextDto
                                {
                                    CallSid = callSid,
                                    StreamSid = streamSid,
                                    Host = host
                                }, orderRecordType, CancellationToken.None));
                            break;
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error(ex, "[AiAssistant] Forward-only WebSocket error, CallSid: {CallSid}", callSid);
        }
    }
}
