using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.Account;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RoleController : ControllerBase
{
    private readonly IMediator _mediator;

    public RoleController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [Route("create"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateResponse))]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCommand command)
    {
        var response = await _mediator.SendAsync<CreateCommand, CreateResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
}