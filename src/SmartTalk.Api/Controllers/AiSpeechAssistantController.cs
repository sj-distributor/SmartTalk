using Serilog;
using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using Twilio.AspNet.Core;
using Twilio.Rest.Api.V2010.Account;
using Twilio.TwiML;
using Twilio.TwiML.Voice;
using Twilio.Types;
using Task = System.Threading.Tasks.Task;

namespace SmartTalk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiSpeechAssistantController : ControllerBase
{
    private readonly IMediator _mediator;

    public AiSpeechAssistantController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Route("call"), HttpGet, HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CallAiSpeechAssistantAsync([FromForm] CallAiSpeechAssistantCommand command)
    {
        command.Host = HttpContext.Request.Host.Host;
        var response = await _mediator.SendAsync<CallAiSpeechAssistantCommand, CallAiSpeechAssistantResponse>(command);

        return response.Data;
    }

    [Route("call/out"), HttpGet, HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CallFromAiSpeechAssistantAsync([FromForm] CallAiSpeechAssistantCommand command)
    {
        var connect = new Connect();
        var response = new VoiceResponse();

        connect.Stream(url: $"wss://{HttpContext.Request.Host.Host}/api/AiSpeechAssistant/connect/{command.From}/{command.To}");
        
        response.Append(connect);
        
        await CallResource.CreateAsync(to: new PhoneNumber(command.To), from: new PhoneNumber(command.From), twiml: new Twiml(response.ToString())).ConfigureAwait(false);

        return Ok(Results.Extensions.TwiML(response));
    }

    [HttpGet("connect/{from}/{to}")]
    public async Task ConnectAiSpeechAssistantAsync(string from, string to)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var command = new ConnectAiSpeechAssistantCommand
            {
                From = from,
                To = to,
                Host = HttpContext.Request.Host.Host,
                TwilioWebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync()
            };
            await _mediator.SendAsync(command);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    [Route("recording/callback"), HttpPost]
    public async Task<IActionResult> ReceivePhoneRecordingStatusCallBackAcyn()
    {
        using (var reader = new StreamReader(Request.Body))
        {
            var body = await reader.ReadToEndAsync();
            
            var query = QueryHelpers.ParseQuery(body);

            Log.Information("Receiving Phone call recording callback command, sid: {CallSid} 、status: {RecordingStatus}、url: {url}", query["CallSid"], query["RecordingStatus"], query["RecordingUrl"]);
            
            await _mediator.SendAsync(new ReceivePhoneRecordingStatusCallbackCommand
            {
                CallSid = query["CallSid"],
                RecordingSid = query["RecordingSid"],
                RecordingUrl = query["RecordingUrl"],
                RecordingTrack = query["RecordingTrack"],
                RecordingStatus = query["RecordingStatus"]
            }).ConfigureAwait(false);
        }
        
        return Ok();
    }
}