using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAudio.Wave;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Commands.AutoTest.SalesPhoneOrder;
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

        // 返回 MP3
        return File(result.Data, "audio/mpeg", "gpt.wav");
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
    
    [Route("dataSet/records"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAutoTestDataSetResponse))]
    public async Task<IActionResult> GetAutoTestDataSetAsync([FromQuery] GetAutoTestDataSetRequest request)
    {
        var response = await _mediator.RequestAsync<GetAutoTestDataSetRequest, GetAutoTestDataSetResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("dataSet/copy"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CopyAutoTestDataSetResponse))]
    public async Task<IActionResult> CopyAutoTestDataItemsAsync([FromBody] CopyAutoTestDataSetCommand command)
    {
        var response = await _mediator.SendAsync<CopyAutoTestDataSetCommand, CopyAutoTestDataSetResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("dataItem/delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteAutoTestDataSetResponse))]
    public async Task<IActionResult> DeleteAutoTestDataItemAsync([FromBody] DeleteAutoTestDataSetCommand command) 
    {
        var response = await _mediator.SendAsync<DeleteAutoTestDataSetCommand, DeleteAutoTestDataSetResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("dataItem/records"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAutoTestDataItemsByIdResponse))]
    public async Task<IActionResult> GetAutoTestDataItemsByIdAsync([FromQuery] GetAutoTestDataItemsByIdRequest request)
    {
        var response = await _mediator.RequestAsync<GetAutoTestDataItemsByIdRequest, GetAutoTestDataItemsByIdResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("dataSet/add/quote"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddAutoTestDataSetByQuoteResponse))]
    public async Task<IActionResult> AddAutoTestDataSetByQuoteAsync([FromBody] AddAutoTestDataSetByQuoteCommand command) 
    {
        var response = await _mediator.SendAsync<AddAutoTestDataSetByQuoteCommand, AddAutoTestDataSetByQuoteResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("task/records"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAutoTestTaskRecordsResponse))]
    public async Task<IActionResult> GetAutoTestTaskRecordsAsync([FromQuery] GetAutoTestTaskRecordsRequest request) 
    {
        var response = await _mediator.RequestAsync<GetAutoTestTaskRecordsRequest, GetAutoTestTaskRecordsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("task/record/mark"), HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MarkAutoTestTaskRecordResponse))]
    public async Task<IActionResult> MarkAutoTestTaskRecordAsync([FromBody] MarkAutoTestTaskRecordCommand command) 
    {
        var response = await _mediator.SendAsync<MarkAutoTestTaskRecordCommand, MarkAutoTestTaskRecordResponse>(command).ConfigureAwait(false);
        
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

    #region Testing Sales Phone Order Scenario

    [AllowAnonymous]
    [Route("sales/phone/order"), HttpPost]
    public async Task<IActionResult> ExecuteSalesPhoneOrderTestAsync([FromBody] ExecuteSalesPhoneOrderTestCommand command) 
    {
        await _mediator.SendAsync(command).ConfigureAwait(false);
        
        return Ok();
    }

    #endregion
}