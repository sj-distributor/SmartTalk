using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.FilesSynchronize;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FilesSynchronizeController : ControllerBase
{
    private readonly IMediator _mediator;

    public FilesSynchronizeController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [Route("create"), HttpPost]
    public async Task<IActionResult> SynchronizeFilesAsync([FromBody] SynchronizeFilesCommand command)
    {
        await _mediator.SendAsync(command).ConfigureAwait(false);
        
        return Ok();
    }
}