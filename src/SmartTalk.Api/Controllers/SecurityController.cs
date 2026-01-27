using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.Security;
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
    
    [Route("create"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateUserAccountResponse))]
    public async Task<IActionResult> CreateUserAccountAsync([FromBody] CreateUserAccountCommand userAccountCommand)
    {
        var response = await _mediator.SendAsync<CreateUserAccountCommand, CreateUserAccountResponse>(userAccountCommand).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("get"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetUserAccountsResponse))]
    public async Task<IActionResult> GetUserAccountsAsync([FromQuery] GetUserAccountsRequest request)
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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteUserAccountsResponse))]
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
    
    [Route("get/role"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetRolesResponse))]
    public async Task<IActionResult> GetRolesAsync([FromQuery] GetRolesRequest request)
    {
        var response = await _mediator.RequestAsync<GetRolesRequest, GetRolesResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("mine/roles"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetCurrentUserRolesResponse))]
    public async Task<IActionResult> GetCurrentUserRoleAsync([FromQuery] GetCurrentUserRolesRequest request)
    {
        var response = await _mediator.RequestAsync<GetCurrentUserRolesRequest, GetCurrentUserRolesResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("language/switch"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwitchLanguageResponse))]
    public async Task<IActionResult> SwitchLanguageAsync([FromBody] SwitchLanguageCommand command)
    {
        var response = await _mediator.SendAsync<SwitchLanguageCommand, SwitchLanguageResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("update/notification"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateUserAccountTaskNotificationResponse))]
    public async Task<IActionResult> UpdateUserAccountAsync([FromBody] UpdateUserAccountTaskNotificationCommand command)
    {
        var response = await _mediator.SendAsync<UpdateUserAccountTaskNotificationCommand, UpdateUserAccountTaskNotificationResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
}