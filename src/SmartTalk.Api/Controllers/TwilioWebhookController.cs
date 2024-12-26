using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.Twilio;
using SmartTalk.Messages.Requests.Twilio;

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

    [Route("get/cdr"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsteriskCdrAsync([FromQuery] GetAsteriskCdrRequest request)
    {
        var response = await _mediator.RequestAsync<GetAsteriskCdrRequest, GetAsteriskCdrResponse>(request);

        return Ok(response);
    }
}