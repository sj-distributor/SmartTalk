using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.Account;
using SmartTalk.Messages.Commands.Authority;

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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateResponse))]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCommand command)
    {
        var response = await _mediator.SendAsync<CreateCommand, CreateResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("get"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAccountsResponse))]
    public async Task<IActionResult> GetAccountsAsync([FromBody] GetAccountsCommand command)
    {
        var response = await _mediator.SendAsync<GetAccountsCommand, GetAccountsResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateResponse))]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateCommand command)
    {
        var response = await _mediator.SendAsync<UpdateCommand, UpdateResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAccountsResponse))]
    public async Task<IActionResult> DeleteAccountsAsync([FromBody] DeleteAccountsCommand command)
    {
        var response = await _mediator.SendAsync<DeleteAccountsCommand, DeleteAccountsResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
}