using Serilog;
using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using SmartTalk.Messages.Commands.Twilio;
using SmartTalk.Messages.DTO.Communication.Twilio;
using SmartTalk.Messages.Enums.Communication.PhoneCall;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TwilioWebhookController : ControllerBase
{
    private readonly IMediator _mediator;

    public TwilioWebhookController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    
    [Route("phonecall"), HttpPost]
    public async Task<IActionResult> HandlePhoneCallStatusCallBackAsync()
    {
        using (var reader = new StreamReader(Request.Body))
        {
            var body = await reader.ReadToEndAsync();
            
            var query = QueryHelpers.ParseQuery(body);
            
            Log.Information("Receiving Phone call status callback command, from: {from} 、to: {to} 、status: {status}", query["From"], query["To"], query["CallStatus"]);
            
            await _mediator.SendAsync(new HandlePhoneCallStatusCallBackCommand
            {
                Provider = PhoneCallProvider.Twilio,
                CallBackMessage = new TwilioPhoneCallStatusCallbackDto
                {
                    To = query["To"],
                    From = query["From"],
                    CallSid = query["CallSid"],
                    Status = query["CallStatus"],
                    Direction = query["Direction"]
                }
            }).ConfigureAwait(false);
        }
        
        return Ok();
    }
}