using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Messages.Commands.PhoneCall;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneCall;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PhoneOrderController : ControllerBase
{
    private readonly IMediator _mediator;

    public PhoneOrderController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [Route("records"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneCallRecordsResponse))]
    public async Task<IActionResult> GetPhoneOrderRecordsAsync([FromQuery] GetPhoneCallRecordsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneCallRecordsRequest, GetPhoneCallRecordsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("conversations"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneCallConversationsResponse))]
    public async Task<IActionResult> GetPhoneOrderConversationsAsync([FromQuery] GetPhoneCallConversationsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneCallConversationsRequest, GetPhoneCallConversationsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("items"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneCallOrderItemsRessponse))]
    public async Task<IActionResult> GetPhoneOrderOrderItemsAsync([FromQuery] GetPhoneCallOrderItemsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneCallOrderItemsRequest, GetPhoneCallOrderItemsRessponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("conversation/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddPhoneOrderConversationsResponse))]
    public async Task<IActionResult> AddPhoneOrderConversationsAsync([FromBody] AddPhoneCallConversationsCommand command) 
    {
        var response = await _mediator.SendAsync<AddPhoneCallConversationsCommand, AddPhoneOrderConversationsResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }

    [HttpPost("record/receive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceivePhoneOrderRecordAsync([FromForm] IFormFile file, [FromForm] string restaurant)
    {
        var ms = new MemoryStream();

        await file.CopyToAsync(ms).ConfigureAwait(false);

        var fileContent = ms.ToArray();
        
        await _mediator.SendAsync(new ReceivePhoneCallRecordCommand { RecordName = file.FileName, RecordContent = fileContent, Restaurant = restaurant}).ConfigureAwait(false);
        
        return Ok();
    }

    [HttpPost("transcription/callback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TranscriptionCallbackAsync(JObject jObject)
    {
        Log.Information("Receive parameter : {jObject}", jObject.ToString());
        
        var transcription = jObject.ToObject<SpeechMaticsGetTranscriptionResponseDto>();
        
        Log.Information("Transcription : {@transcription}", transcription);
        
        await _mediator.SendAsync(new HandleTranscriptionCallbackCommand { Transcription = transcription }).ConfigureAwait(false);

        return Ok();
    }

    [Route("manual/order"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddOrUpdateManualOrderResponse))]
    public async Task<IActionResult> AddOrUpdateManualOrderAsync([FromBody] AddOrUpdateManualOrderCommand command)
    {
        var response = await _mediator.SendAsync<AddOrUpdateManualOrderCommand, AddOrUpdateManualOrderResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
}