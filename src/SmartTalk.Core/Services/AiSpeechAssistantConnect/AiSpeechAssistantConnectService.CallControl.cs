using System.Net.WebSockets;
using System.Text.Json;
using Serilog;
using SmartTalk.Core.Services.WebSockets;
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

        Log.Information("[AiAssistant] Forward started, CallSid: {CallSid}, StreamSid: {StreamSid}", _ctx.CallSid, _ctx.StreamSid);

        TriggerTwilioRecordingPhoneCall();
        TransferHumanService(forwardPhoneNumber);
    }

    private void HandleForwardStop()
    {
        GenerateRecordFromCall(new AiSpeechAssistantStreamContextDto
        {
            CallSid = _ctx.CallSid, StreamSid = _ctx.StreamSid, Host = _ctx.Host, LastUserInfo = _ctx.LastUserInfo
        });
    }
}
