using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Requests.Twilio;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TwilioController : ControllerBase
{
    private readonly IMediator _mediator;

    public TwilioController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("phone-numbers/migrate")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MigrateIncomingPhoneNumberResponse))]
    public async Task<IActionResult> MigrateIncomingPhoneNumberAsync([FromBody] MigrateIncomingPhoneNumberRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.RequestAsync<MigrateIncomingPhoneNumberRequest, MigrateIncomingPhoneNumberResponse>(request, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }
}
