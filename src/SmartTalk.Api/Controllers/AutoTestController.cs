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
    
    [Route("task"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAutoTestTaskResponse))]
    public async Task<IActionResult> GetAutoTestTasksAsync([FromQuery] GetAutoTestTaskRequest request)
    {
        var response = await _mediator.RequestAsync<GetAutoTestTaskRequest, GetAutoTestTaskResponse>(request).ConfigureAwait(false);
    
        return Ok(response);
    }
    
    [Route("task"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateAutoTestTaskResponse))]
    public async Task<IActionResult> CreateAutoTestTaskAsync([FromBody] CreateAutoTestTaskCommand command) 
    {
        var response = await _mediator.SendAsync<CreateAutoTestTaskCommand, CreateAutoTestTaskResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("task"), HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateAutoTestTaskResponse))]
    public async Task<IActionResult> UpdateAutoTestTaskAsync([FromBody] UpdateAutoTestTaskCommand command) 
    {
        var response = await _mediator.SendAsync<UpdateAutoTestTaskCommand, UpdateAutoTestTaskResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("task"), HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteAutoTestTaskResponse))]
    public async Task<IActionResult> DeleteAutoTestTaskAsync([FromBody] DeleteAutoTestTaskCommand command) 
    {
        var response = await _mediator.SendAsync<DeleteAutoTestTaskCommand, DeleteAutoTestTaskResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("autoTestDataSet/records"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAutoTestDataSetResponse))]
    public async Task<IActionResult> GetAutoTestDataSetAsync([FromQuery] GetAutoTestDataSetRequest request)
    {
        var response = await _mediator.RequestAsync<GetAutoTestDataSetRequest, GetAutoTestDataSetResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("autoTestDataSet/copy"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CopyAutoTestDataSetResponse))]
    public async Task<IActionResult> CopyAutoTestDataItemsAsync([FromQuery] CopyAutoTestDataSetRequest request)
    {
        var response = await _mediator.RequestAsync<CopyAutoTestDataSetRequest, CopyAutoTestDataSetResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("autoTestDataSet/delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteAutoTestDataSetResponse))]
    public async Task<IActionResult> DeleteAutoTestDataSetAsync([FromBody] DeleteAutoTestDataSetCommand command) 
    {
        var response = await _mediator.SendAsync<DeleteAutoTestDataSetCommand, DeleteAutoTestDataSetResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("autoTestDataItem/records"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAutoTestDataItemsByIdResponse))]
    public async Task<IActionResult> GetAutoTestDataItemsByIdAsync([FromQuery] GetAutoTestDataItemsByIdRequest request)
    {
        var response = await _mediator.RequestAsync<GetAutoTestDataItemsByIdRequest, GetAutoTestDataItemsByIdResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("autoTestDataSet/add/quote"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddAutoTestDataSetByQuoteResponse))]
    public async Task<IActionResult> AddAutoTestDataSetByQuoteAsync([FromBody] AddAutoTestDataSetByQuoteCommand command) 
    {
        var response = await _mediator.SendAsync<AddAutoTestDataSetByQuoteCommand, AddAutoTestDataSetByQuoteResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
}