using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.Sales;
using SmartTalk.Messages.Requests.Sales;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SalesAutoCreateController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesAutoCreateController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Route("sync-crm"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SyncCrmSalesAutoCreateResponse))]
    public async Task<IActionResult> SyncCrmAsync([FromBody] SyncCrmSalesAutoCreateCommand command, CancellationToken cancellationToken)
    {
        command.IsManual = true;
        var response = await _mediator.SendAsync<SyncCrmSalesAutoCreateCommand, SyncCrmSalesAutoCreateResponse>(command, cancellationToken).ConfigureAwait(false);
        return Ok(response);
    }
}
