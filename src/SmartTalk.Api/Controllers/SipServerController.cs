using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.SipServer;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SipServerController : ControllerBase
{
    private readonly IMediator _mediator;

    public SipServerController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [Route("backup"), HttpPost]
    public async Task<IActionResult> BackupSipServerDataAsync([FromBody] BackupSipServerDataCommand command)
    {
        await _mediator.SendAsync(command).ConfigureAwait(false);
        
        return Ok();
    }
    
    [Route("host/servers/add"), HttpPost]
    public async Task<IActionResult> AddSipHostServersAsync([FromBody] AddSipHostServersCommand command)
    {
        var response = await _mediator.SendAsync<AddSipHostServersCommand, AddSipHostServersResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("backup/servers/add"), HttpPost]
    public async Task<IActionResult> AddSipBackupServersAsync([FromBody] AddSipBackupServersCommand command)
    {
        var response = await _mediator.SendAsync<AddSipBackupServersCommand, AddSipBackupServersResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("update/dns"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateDomainIpResponse))]
    public async Task<IActionResult> UpdateDomainIpAsync([FromBody] UpdateDomainIpCommand command)
    {
        var response = await _mediator.SendAsync<UpdateDomainIpCommand, UpdateDomainIpResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
}