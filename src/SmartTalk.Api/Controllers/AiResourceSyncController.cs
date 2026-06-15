using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.AiResourceSync;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AiResourceSyncController : ControllerBase
{
    private readonly IMediator _mediator;

    public AiResourceSyncController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Route("sync-crm"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AiResourceSyncResponse))]
    public async Task<IActionResult> SyncCrmAsync([FromBody] AiResourceSyncCommand command, CancellationToken cancellationToken)
    {
        command.IsManual = true;
        
        var response = await _mediator.SendAsync<AiResourceSyncCommand, AiResourceSyncResponse>(command, cancellationToken).ConfigureAwait(false);
        return Ok(response);
    }
}
