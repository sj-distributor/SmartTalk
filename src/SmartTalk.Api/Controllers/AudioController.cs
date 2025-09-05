using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.SpeechMatics;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AudioController : ControllerBase
{
    private readonly IMediator _mediator;

    public AudioController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [Route("analyze"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AnalyzeAudioAsync([FromBody] AnalyzeAudioCommand command, CancellationToken cancellationToken)
    {
        var response = await _mediator.SendAsync<AnalyzeAudioCommand, AnalyzeAudioResponse>(command, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }
}