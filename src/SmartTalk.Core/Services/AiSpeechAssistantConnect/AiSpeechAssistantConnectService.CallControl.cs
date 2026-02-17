using System.Net.WebSockets;
using System.Text.Json;
using Mediator.Net;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.WebSockets;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task HandleForwardOnlyAsync(string forwardPhoneNumber, CancellationToken cancellationToken)
    {
        try
        {
            await WebSocketReader.RunAsync(_ctx.TwilioWebSocket, message =>
            {
                using var doc = JsonDocument.Parse(message);
                var eventType = doc.RootElement.GetProperty("event").GetString();

                switch (eventType)
                {
                    case "start":
                        HandleForwardStart(doc, forwardPhoneNumber);
                        break;
                    case "stop":
                        HandleForwardStop();
                        break;
                }

                return Task.CompletedTask;
                
            }, cancellationToken).ConfigureAwait(false);
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
