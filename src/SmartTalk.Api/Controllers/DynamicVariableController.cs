using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Requests.DynamicInterface;

namespace SmartTalk.Api.Controllers;
//
// [Authorize]
// [ApiController]
[Route("api/[controller]")]
public class DynamicInterfaceController : ControllerBase
{
    private readonly IMediator _mediator;

    public DynamicInterfaceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Route("dynamic/tree"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetDynamicInterfaceTreeResponse))]
    public async Task<IActionResult> GetDynamicInterfaceTreeAsync([FromQuery] GetDynamicInterfaceTreeRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.RequestAsync<GetDynamicInterfaceTreeRequest, GetDynamicInterfaceTreeResponse>(request, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }
}