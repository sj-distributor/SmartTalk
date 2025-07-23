using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.SpeechMatics;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SpeechMaticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SpeechMaticsController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [Route("speech/text"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SpeechToText([FromBody] AudioToTextCommand command, CancellationToken cancellationToken)
    {
        await _mediator.SendAsync(command, cancellationToken).ConfigureAwait(false);

        return Ok();
    }

}