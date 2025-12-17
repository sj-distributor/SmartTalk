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
using SmartTalk.Message.Commands.Printer;
using SmartTalk.Messages.Commands.Printer;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Api.Controllers;

[Authorize]
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

    [AllowAnonymous]
    [HttpPost, Route("poll")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPrinterJobAvailableResponse))]
    public async Task<IActionResult> PollAsync([FromBody] PollRequest pollRequest, Guid? token ,CancellationToken cancellationToken)
    {
        if (!token.HasValue) return Ok(new GetPrinterJobAvailableResponse{Code = HttpStatusCode.Unauthorized, Msg = "Token is null"});
            
        var request = new GetPrinterJobAvailableRequest() {PrinterMac = pollRequest.printerMAC, Token = token.Value};
            
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

    [AllowAnonymous]
    [HttpGet, Route("poll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync([FromQuery] PrinterJobDto dto)
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
        
    [AllowAnonymous]
    [HttpDelete, Route("poll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync([FromQuery] ConfirmPrinterJobDto dto)
    {
        Log.Information("ConfirmPrinterJobDto: {@ConfirmPrinterJobDto}", dto);
            
        await _mediator.SendAsync(_mapper.Map<ConfirmPrinterJobCommand>(dto)).ConfigureAwait(false);

        return Ok();
    }
        
    [AllowAnonymous]
    [HttpPost, Route("printTest")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PrintTestResponse))]
    public async Task<IActionResult> PrintTestAsync([FromBody] PrintTestCommand command)
    {
        var response = await _mediator.SendAsync<PrintTestCommand, PrintTestResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [HttpGet, Route("printLog")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetMerchPrinterLogResponse))]
    public async Task<IActionResult> GetMerchPrinterLogAsync([FromQuery] GetMerchPrinterLogRequest request)
    {
        var response = await _mediator.RequestAsync<GetMerchPrinterLogRequest, GetMerchPrinterLogResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    [HttpPost, Route("merchPrinter/add")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddMerchPrinterResponse))]
    public async Task<IActionResult> AddMerchPrinterAsync([FromBody] AddMerchPrinterCommand command)
    {
        var response = await _mediator.SendAsync<AddMerchPrinterCommand, AddMerchPrinterResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }

    [HttpPost, Route("merchPrinter/update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateMerchPrinterResponse))]
    public async Task<IActionResult> UpdateMerchPrinterAsync([FromBody] UpdateMerchPrinterCommand command)
    {
        var response = await _mediator.SendAsync<UpdateMerchPrinterCommand, UpdateMerchPrinterResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }

    [HttpDelete, Route("merchPrinter/delete")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteMerchPrinterResponse))]
    public async Task<IActionResult> DeleteMerchPrinterAsync([FromBody] DeleteMerchPrinterCommand command)
    {
        var response = await _mediator.SendAsync<DeleteMerchPrinterCommand, DeleteMerchPrinterResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }

    [HttpGet, Route("merchPrinters")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetMerchPrintersResponse))]
    public async Task<IActionResult> GetMerchPrintersAsync([FromQuery] GetMerchPrintersRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.RequestAsync<GetMerchPrintersRequest, GetMerchPrintersResponse>(request, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }
    
    [HttpPost, Route("merchPrinterOrder/retry")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MerchPrinterOrderRetryResponse))]
    public async Task<IActionResult> MerchPrinterOrderRetryAsync([FromBody] MerchPrinterOrderRetryCommand request, CancellationToken cancellationToken)
    {
        var response = await _mediator.SendAsync<MerchPrinterOrderRetryCommand, MerchPrinterOrderRetryResponse>(request, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }
}  