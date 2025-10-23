using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AutoTestController : ControllerBase
{
    private readonly IMediator _mediator;

    public AutoTestController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [Route("run"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AutoTestRunningResponse))]
    public async Task<IActionResult> AutoTestAsync([FromBody] AutoTestRunningCommand command) 
    {
        var response = await _mediator.SendAsync<AutoTestRunningCommand, AutoTestRunningResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("import"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AutoTestImportDataResponse))]
    public async Task<IActionResult> AutoTestImportDataAsync([FromBody] AutoTestImportDataCommand command) 
    {
        var response = await _mediator.SendAsync<AutoTestImportDataCommand, AutoTestImportDataResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("conversation"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AutoTestConversationAudioProcessReponse))]
    public async Task<IActionResult> AutoTestConversationAudioProcessAsync([FromBody] AutoTestConversationAudioProcessCommand command) 
    {
        var response = await _mediator.SendAsync<AutoTestConversationAudioProcessCommand, AutoTestConversationAudioProcessReponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
}