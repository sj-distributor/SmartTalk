using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

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
    
    [Route("testTask"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAutoTestTestTaskResponse))]
    public async Task<IActionResult> GetAutoTestTestTasksAsync([FromQuery]  GetAutoTestTestTaskRequest command)
    {
        var response = await _mediator.RequestAsync<GetAutoTestTestTaskRequest, GetAutoTestTestTaskResponse>(command).ConfigureAwait(false);
    
        return Ok(response);
    }
    
    [Route("testTask/create"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateAutoTestTestTaskResponse))]
    public async Task<IActionResult> CreateAutoTestTestTaskAsync([FromBody] CreateAutoTestTestTaskCommand command) 
    {
        var response = await _mediator.SendAsync<CreateAutoTestTestTaskCommand, CreateAutoTestTestTaskResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("testTask/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateAutoTestTestTaskResponse))]
    public async Task<IActionResult> UpdateAutoTestTestTaskAsync([FromBody] UpdateAutoTestTestTaskCommand command) 
    {
        var response = await _mediator.SendAsync<UpdateAutoTestTestTaskCommand, UpdateAutoTestTestTaskResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("testTask/delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteAutoTestTestTaskResponse))]
    public async Task<IActionResult> DeleteAutoTestTestTaskAsync([FromBody] DeleteAutoTestTestTaskCommand command) 
    {
        var response = await _mediator.SendAsync<DeleteAutoTestTestTaskCommand, DeleteAutoTestTestTaskResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
}