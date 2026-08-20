using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.Translation;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TranslationController : ControllerBase
{
    private readonly IMediator _mediator;

    public TranslationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Route("batch"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BatchTranslateResponse))]
    public async Task<IActionResult> BatchTranslateAsync([FromBody] BatchTranslateCommand command, CancellationToken cancellationToken)
    {
        var response = await _mediator.SendAsync<BatchTranslateCommand, BatchTranslateResponse>(command, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }
}
