using Serilog;
using System.Net;
using AutoMapper;
using System.Text;
using Mediator.Net;
using Newtonsoft.Json;
using System.Diagnostics;
using StarMicronics.CloudPrnt;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Dto.Printer;
using StarMicronics.CloudPrnt.CpMessage;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.Printer;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrinterController : ControllerBase
{
    private readonly IMapper _mapper;
    private readonly IMediator _mediator;

    public PrinterController(IMapper mapper, IMediator mediator)
    {
        _mapper = mapper;
        _mediator = mediator;
    }

    [HttpPost, AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPrinterJobAvailableResponse))]
    public async Task<IActionResult> Post([FromBody] PollRequest pollRequest, Guid? token ,CancellationToken cancellationToken)
    {
        if (!token.HasValue) return Ok(new GetPrinterJobAvailableResponse{Code = HttpStatusCode.Unauthorized, Msg = "Token is null"});
            
        var request = new GetPrinterJobAvailableRequest() {PrinterMac = pollRequest.printerMAC,Token = token.Value};
            
        var response = await _mediator.RequestAsync<GetPrinterJobAvailableRequest, GetPrinterJobAvailableResponse>(request, cancellationToken);

        Log.Information("Get printer job available response: {@response}", response);
            
        if (response == null)
            return Ok(new GetPrinterJobAvailableResponse{Code = HttpStatusCode.Unauthorized, Msg = "No printer job"});
            
        var recordCommand = JsonConvert.DeserializeObject<RecordPrinterStatusCommand>(JsonConvert.SerializeObject(pollRequest.DecodedStatus));
        
        recordCommand.Token = token.Value;
        recordCommand.PrinterMac = pollRequest.printerMAC;
        
        await _mediator.SendAsync(recordCommand).ConfigureAwait(false);

        return Ok(response);
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] PrinterJobDto dto)
    {
        if (dto.Token == Guid.Empty)
        {
            var jobToken = Request.Headers["X-Star-Token"];
            if (!string.IsNullOrEmpty(jobToken))
                dto.Token = Guid.Parse(jobToken);
        }

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
        var command = _mapper.Map<PrinterJobCommand>(dto);
        var response = await _mediator.SendAsync<PrinterJobCommand, PrinterJobResponse>(command).ConfigureAwait(false);

        Log.Information("Printer Get PrinterJobCommand run{@Ms}", stopwatch.ElapsedMilliseconds);
        
        stopwatch.Restart();

        if (response?.MerchPrinterOrder == null)
            return new EmptyResult();

        string imageUrl;

        if (response.MerchPrinterOrder.IsPrintTest())
            imageUrl = "https://cdn.protonsystem.io/276f95f3-04a1-11ec-82c4-a4bb6ddba40d.jpg";
        else
        {
            var url = await _mediator.RequestAsync<UploadOrderPrintImageAndUpdatePrintUrlRequest, UploadOrderPrintImageAndUpdatePrintUrlResponse>(
                new UploadOrderPrintImageAndUpdatePrintUrlRequest { JobToken = command.JobToken });

            imageUrl = url.ImageUrl;
        }
            
        Log.Information("Printer Get UploadOrderPrintImageAndUpdatePrintUrlRequest run{@Ms}", stopwatch.ElapsedMilliseconds);
        try
        {
            stopwatch.Restart();

            var content = $@"[image: url {imageUrl};width 100%;min-width 30mm][cut: feed; partial]";
            var jobData = Encoding.UTF8.GetBytes(content);

            Log.Information("jobData long:{jobData}", jobData.Length);
                
            var outputFormat = command.Type;
                
            var ms = new MemoryStream();
            Document.Convert(jobData, "text/vnd.star.markup", ms, outputFormat, new ConversionOptions());
            ms.Position = 0;
                
            stopwatch.Stop();
            Log.Information("Printer Get Convert run{@Ms}", stopwatch.ElapsedMilliseconds);
                            
            return File(ms, outputFormat);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Convert failed");
            throw;
        }
    }
        
    [HttpDelete, AllowAnonymous]
    public async Task<IActionResult> Delete([FromQuery] ConfirmPrinterJobDto dto)
    {
        Log.Information("ConfirmPrinterJobDto: {@ConfirmPrinterJobDto}", dto);
            
        await _mediator.SendAsync(_mapper.Map<ConfirmPrinterJobCommand>(dto)).ConfigureAwait(false);

        return Ok();
    }
        
    [AllowAnonymous]
    [HttpPost, Route("printTest")]
    public async Task<IActionResult> PrintTest(PrintTestCommand command)
    {
        await _mediator.SendAsync(command).ConfigureAwait(false);
        
        return Ok();
    }
}  