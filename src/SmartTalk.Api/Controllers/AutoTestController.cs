using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAudio.Wave;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AutoTestController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAutoTestService _autoTestService;

    public AutoTestController(IMediator mediator, IAutoTestService autoTestService)
    {
        _mediator = mediator;
        _autoTestService = autoTestService;
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
    public async Task<IActionResult> AutoTestConversationAudioProcessAsync([FromForm] List<IFormFile> mp3Files, [FromForm] string prompt, CancellationToken cancellationToken)
    {
        if (mp3Files == null || mp3Files.Count == 0)
            return BadRequest("没有上传音频文件");

        var customerMp3List = new List<byte[]>();

        foreach (var file in mp3Files)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            customerMp3List.Add(ms.ToArray());
        }

        var result = await _autoTestService.AutoTestConversationAudioProcessAsync(new AutoTestConversationAudioProcessCommand
        {
            CustomerAudioList = customerMp3List,
            Prompt = prompt
        }, cancellationToken).ConfigureAwait(false);

        return File(result.Data, "audio/mpeg", "conversation.mp3");
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
}