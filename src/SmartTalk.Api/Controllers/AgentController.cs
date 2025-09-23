using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.Agent;
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
    
    [Route("surface/agents"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetSurfaceAgentsResponse))]
    public async Task<IActionResult> GetSurfaceAgentsAsync([FromQuery] GetSurfaceAgentsRequest request)
    {
        var response = await _mediator.RequestAsync<GetSurfaceAgentsRequest, GetSurfaceAgentsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("initialize"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddAgentResponse))]
    public async Task<IActionResult> InitializeAgentAsync([FromBody] AddAgentCommand command)
    {
        var response = await _mediator.SendAsync<AddAgentCommand, AddAgentResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("modify"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateAgentResponse))]
    public async Task<IActionResult> ModifyAgentAsync([FromBody] UpdateAgentCommand command)
    {
        var response = await _mediator.SendAsync<UpdateAgentCommand, UpdateAgentResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("delete"), HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteAgentResponse))]
    public async Task<IActionResult> DeleteAgentAsync([FromBody] DeleteAgentCommand command)
    {
        var response = await _mediator.SendAsync<DeleteAgentCommand, DeleteAgentResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
}