using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IMediator _mediator;

    public AgentController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [Route("agents"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAgentsResponse))]
    public async Task<IActionResult> GetAgentsAsync([FromQuery] GetAgentsRequest request)
    {
        var response = await _mediator.RequestAsync<GetAgentsRequest, GetAgentsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
}