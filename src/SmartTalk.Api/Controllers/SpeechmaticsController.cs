using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Smarties.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.Speechmatics;

namespace SmartTalk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpeechmaticsController : ControllerBase
{
    private readonly ISpeechmaticsClient _speechmaticsClient;

    public SpeechmaticsController(ISpeechmaticsClient speechmaticsClient)
    {
        _speechmaticsClient = speechmaticsClient;
    }
    
    [Route("Create/Job"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    public async Task<IActionResult> CreateJobAsync([FromForm] SpeechmaticsCreateJobRequestDto speechmaticsCreateJobRequestDto)
    {
        string filePath = "";
        byte[] data = System.IO.File.ReadAllBytes(filePath);
        var response = await _speechmaticsClient.CreateJobAsync(speechmaticsCreateJobRequestDto, data,null, CancellationToken.None);
        return Ok(response);
    }
    
    [Route("Get/AllJobs"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SpeechmaticsGetAllJobsResponseDto))]
    public async Task<IActionResult> GetAlljobsAsync()
    {
        var response = await _speechmaticsClient.GetAllJobsAsync(CancellationToken.None).ConfigureAwait(false);
        return Ok(response);
    }
    
    [Route("Get/JobDetail"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SpeechmaticsGetJobDetailResponseDto))]
    public async Task<IActionResult> GetJobDetailAsync([FromQuery] string id)
    {
        var response = await _speechmaticsClient.GetJobDetailAsync(id,CancellationToken.None).ConfigureAwait(false);
        return Ok(response);
    }
    
    [Route("Delete/Job"), HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SpeechmaticsDeleteJobResponseDto))]
    public async Task<IActionResult> DeleteJobAsync([FromQuery] string id)
    {
        var response = await _speechmaticsClient.DeleteJobAsync(id,CancellationToken.None).ConfigureAwait(false);
        return Ok(response);
    }
    
    [Route("Get/Transcript"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SpeechmaticsGetTranscriptionResponseDto))]
    public async Task<IActionResult> GetTranscriptFormAsync([FromQuery] string id, string format)
    {
        var response = await _speechmaticsClient.GetTranscriptAsync(id, format, CancellationToken.None).ConfigureAwait(false);
        return Ok(response);
    }

    [Route("Get/AlignedText"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    public async Task<IActionResult> GetAlignedTextAsync([FromQuery] string id, string tags)
    {
        var response = await _speechmaticsClient.GetAlignedTextAsync(id, tags, CancellationToken.None).ConfigureAwait(false);
        return Ok(response);
    }
    
    [Route("Get/Usage"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SpeechmaticsGetUsageResponseDto))]
    public async Task<IActionResult> GetUsageAsync([FromQuery] SpeechmaticsGetUsageRequestDto speechmaticsGetUsageRequestDto)
    {
        var response = await _speechmaticsClient.GetUsageStatisticsAsync(speechmaticsGetUsageRequestDto, CancellationToken.None).ConfigureAwait(false);
        return Ok(response);
    }
}