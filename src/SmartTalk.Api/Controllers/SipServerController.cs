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
}