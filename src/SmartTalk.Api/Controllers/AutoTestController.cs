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
    public async Task<IActionResult> AutoTestConversationAudioProcessAsync([FromForm] List<IFormFile> wavFiles, [FromForm] string prompt, CancellationToken cancellationToken)
    {
        var customerAudioList = new List<byte[]>();

        foreach (var file in wavFiles)
        {
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                customerAudioList.Add(ms.ToArray());
            }
        }

        var result = await _autoTestService.AutoTestConversationAudioProcessAsync(
            new AutoTestConversationAudioProcessCommand()
            {
                CustomerAudioList = customerAudioList,
                Prompt = prompt
            }, cancellationToken).ConfigureAwait(false);

        return File(result.Data, "audio/wav", "result.wav");
    }
    
    [HttpPost("pcm-to-wav")]
    public async Task<IActionResult> ConvertPcmToWavAsync([FromForm] IFormFile pcmFile, [FromForm] int sampleRate = 16000, [FromForm] int bitsPerSample = 16, [FromForm] int channels = 1, CancellationToken cancellationToken = default)
    {
        if (pcmFile == null || pcmFile.Length == 0)
        {
            return BadRequest("PCM file is required");
        }

        byte[] pcmData;
        using (var ms = new MemoryStream())
        {
            await pcmFile.CopyToAsync(ms, cancellationToken);
            pcmData = ms.ToArray();
        }

        var wavData = PcmToWav(pcmData, sampleRate, bitsPerSample, channels);
        
        return File(wavData, "audio/wav", "converted.wav");
    }

    [HttpPost("extract-pcm-from-wav")]
    public async Task<IActionResult> ExtractPcmFromWavAsync([FromForm] IFormFile wavFile, CancellationToken cancellationToken = default)
    {
        if (wavFile == null || wavFile.Length == 0)
        {
            return BadRequest("WAV file is required");
        }

        byte[] wavData;
        using (var ms = new MemoryStream())
        {
            await wavFile.CopyToAsync(ms, cancellationToken);
            wavData = ms.ToArray();
        }

        var pcmData = ExtractPcmFromWav(wavData);
        
        return File(pcmData, "application/octet-stream", "extracted.pcm");
    }

    [HttpPost("concat-pcm-segments")]
    public async Task<IActionResult> ConcatPcmSegmentsAsync([FromForm] List<IFormFile> pcmFiles, CancellationToken cancellationToken = default)
    {
        if (pcmFiles == null || !pcmFiles.Any())
        {
            return BadRequest("At least one PCM file is required");
        }

        var segments = new List<byte[]>();
        foreach (var file in pcmFiles)
        {
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, cancellationToken);
                segments.Add(ms.ToArray());
            }
        }

        var concatenatedPcm = ConcatPcmSegments(segments);
        
        return File(concatenatedPcm, "application/octet-stream", "concatenated.pcm");
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
    
    private static byte[] PcmToWav(byte[] pcmData, int sampleRate, int bitsPerSample, int channels)
    {
        using var ms = new MemoryStream();
        var waveFormat = new WaveFormat(sampleRate, bitsPerSample, channels);
        using (var writer = new WaveFileWriter(ms, waveFormat))
        {
            writer.Write(pcmData, 0, pcmData.Length);
            writer.Flush();
        }
        return ms.ToArray();
    }
    
    private static byte[] ExtractPcmFromWav(byte[] wavData)
    {
        using var ms = new MemoryStream(wavData);
        using var rdr = new WaveFileReader(ms);
        using var pcmStream = new MemoryStream();
        rdr.CopyTo(pcmStream);
        return pcmStream.ToArray();
    }

    private static byte[] ConcatPcmSegments(List<byte[]> segments)
    {
        int totalLength = segments.Sum(s => s.Length);
        byte[] result = new byte[totalLength];
        int offset = 0;
        foreach (var s in segments)
        {
            Buffer.BlockCopy(s, 0, result, offset, s.Length);
            offset += s.Length;
        }

        return result;
    }
}