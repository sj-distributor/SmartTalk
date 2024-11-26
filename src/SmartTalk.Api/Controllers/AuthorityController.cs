using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.Account;
using SmartTalk.Messages.Commands.Authority;
using SmartTalk.Messages.Requests.Authority;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AuthorityController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthorityController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Route("create"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateUserAccountResponse))]
    public async Task<IActionResult> CreateUserAccountAsync([FromBody] CreateUserAccountCommand userAccountCommand)
    {
        var response = await _mediator.SendAsync<CreateUserAccountCommand, CreateUserAccountResponse>(userAccountCommand).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("get"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetUserAccountsResponse))]
    public async Task<IActionResult> GetUserAccountAccountsAsync([FromQuery] GetUserAccountsRequest request)
    {
        var response = await _mediator.RequestAsync<GetUserAccountsRequest, GetUserAccountsResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateUserAccountResponse))]
    public async Task<IActionResult> UpdateUserAccountAsync([FromBody] UpdateUserAccountCommand command)
    {
        var response = await _mediator.SendAsync<UpdateUserAccountCommand, UpdateUserAccountResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteUserAccountsCommand))]
    public async Task<IActionResult> DeleteUserAccountsAsync([FromBody] DeleteUserAccountsCommand command)
    {
        var response = await _mediator.SendAsync<DeleteUserAccountsCommand, DeleteUserAccountsResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("copy"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetUserAccountInfoResponse))]
    public async Task<IActionResult> GetUserAccountInfoAsync([FromQuery] GetUserAccountInfoRequest request)
    {
        var response = await _mediator.RequestAsync<GetUserAccountInfoRequest, GetUserAccountInfoResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }
}