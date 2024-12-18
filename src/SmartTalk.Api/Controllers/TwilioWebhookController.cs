using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.Twilio;

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
    public async Task<IActionResult> HandlePhoneCallStatusCallBackAsync([FromBody] HandlePhoneCallStatusCallBackCommand command)
    {
        await _mediator.SendAsync(command).ConfigureAwait(false);
        
        return Ok();
    }
}