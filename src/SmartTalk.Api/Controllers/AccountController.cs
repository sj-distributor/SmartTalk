using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.Account;
using SmartTalk.Messages.Requests.Account;

namespace SmartTalk.Api.Controllers;

[ApiController]
[Route("auth")]
public class AccountController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Route("login"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginResponse))]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        var response = await _mediator.RequestAsync<LoginRequest, LoginResponse>(request);
        
        return Ok(response);
    }

    [Authorize]
    [Route("create"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateResponse))]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCommand command)
    {
        var response = await _mediator.SendAsync<CreateCommand, CreateResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
}