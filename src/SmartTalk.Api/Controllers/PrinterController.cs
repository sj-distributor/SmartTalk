using Serilog;
using System.Net;
using AutoMapper;
using System.Text;
using Mediator.Net;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Dto.Printer;
using StarMicronics.StarDocumentMarkup;
using StarMicronics.CloudPrnt.CpMessage;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.Printer;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PrinterController : ControllerBase
{
        private readonly IMediator _mediator;
        private readonly IMapper _mapper;

        public PrinterController(IMediator mediator, IMapper mapper)
        {
            _mediator = mediator;
            _mapper = mapper;
        }

        [HttpPost, AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPrinterJobAvailableResponse))]
        public async Task<IActionResult> Post([FromBody] PollRequest pollRequest, Guid? token ,CancellationToken cancellationToken)
        {
            if (!token.HasValue) return Ok(new GetPrinterJobAvailableResponse{Code = HttpStatusCode.Unauthorized, Msg = "Token is null"});
            
            Log.Information("PollReqeust:{@pollRequest}, Token: {token}", pollRequest, token);
            
            var request = new GetPrinterJobAvailableRequest() {PrinterMac = pollRequest.printerMAC,Token = token.Value};

            Log.Information("Get printer job available request: {@request}", request);
            
            var response = await _mediator.RequestAsync<GetPrinterJobAvailableRequest, GetPrinterJobAvailableResponse>(request, cancellationToken);

            Log.Information("Get printer job available response: {@response}", response);
            
            if (response == null)
                return Ok(new GetPrinterJobAvailableResponse{Code = HttpStatusCode.Unauthorized, Msg = "No printer job"});
            
            var recordCommand = JsonConvert.DeserializeObject<RecordPrinterStatusCommand>(
                JsonConvert.SerializeObject(pollRequest.DecodedStatus));
            recordCommand.PrinterMac = pollRequest.printerMAC;
            recordCommand.Token = token.Value;
            await _mediator.SendAsync(recordCommand);

            return Ok(response);
        }

        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> Get([FromQuery] PrinterJobDto dto)
        {
            //Compatible with different firmware versions  
            if (dto.Token == Guid.Empty)
            {
                var jobToken = Request.Headers["X-Star-Token"];
                if (!string.IsNullOrEmpty(jobToken))
                {
                    dto.Token = Guid.Parse(jobToken);
                }
            }
            
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            //get order 
            var command = _mapper.Map<PrinterJobCommand>(dto);
            var response = await _mediator.SendAsync<PrinterJobCommand, PrinterJobResponse>(command);
            
            Log.Information("Printer Get PrinterJobCommand run{@Ms}", stopwatch.ElapsedMilliseconds);
            stopwatch.Restart();

            if (response?.MerchPrinterOrder == null)
            {
                return new EmptyResult();
            }
            
            string imageUrl;
            
            if (response.MerchPrinterOrder.IsPrintTest())
            {
                imageUrl = "https://cdn.protonsystem.io/276f95f3-04a1-11ec-82c4-a4bb6ddba40d.jpg";
            }
            else
            {
                var url =
                    await _mediator
                        .RequestAsync<UploadOrderPrintImageToQiNiuAndUpdatePrintUrlRequest,
                            UploadOrderPrintImageToQiNiuAndUpdatePrintUrlResponse>(
                            new UploadOrderPrintImageToQiNiuAndUpdatePrintUrlRequest() {JobToken = command.JobToken});

                imageUrl = url.ImageUrl;
            }
            
            Log.Information("Printer Get UploadOrderPrintImageToQiNiuAndUpdatePrintUrlRequest run{@Ms}", stopwatch.ElapsedMilliseconds);
            stopwatch.Restart();

            var content =
                $@"[image: url {imageUrl};width 100%;min-width 30mm][cut: feed; partial]";

            var jobData = Encoding.UTF8.GetBytes(content);
            // get the requested output media type from the query string
            string outputFormat = command.Type;
            // set the response media type, and output the converted job to the response body
            Response.ContentType = outputFormat;
            Document.Convert(jobData, "text/vnd.star.markup", Response.Body, outputFormat, new ConversionOptions() { });
            stopwatch.Stop();
            Log.Information("Printer Get Convert run{@Ms}", stopwatch.ElapsedMilliseconds);
            
            return new EmptyResult();
        }
        
        [HttpDelete, AllowAnonymous]
        public async Task<IActionResult> Delete([FromQuery] ConfirmPrinterJobDto dto)
        {
            await _mediator.SendAsync(_mapper.Map<ConfirmPrinterJobCommand>(dto));

            return Ok();
        }
        
        [AllowAnonymous]
        [HttpPost, Route("printTest")]
        public async Task<IActionResult> PrintTest(PrintTestCommand command)
        {
            await _mediator.SendAsync(command);
            return Ok();
        }
}  