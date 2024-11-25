using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Requests.Security;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SecurityController : ControllerBase
{
     private readonly IMediator _mediator;

    public SecurityController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [Route("mine/roles"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetCurrentUserRolesResponse))]
    public async Task<IActionResult> GetCurrentUserRoleAsync([FromQuery] GetCurrentUserRolesRequest request)
    {
        var response = await _mediator.RequestAsync<GetCurrentUserRolesRequest, GetCurrentUserRolesResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }
}