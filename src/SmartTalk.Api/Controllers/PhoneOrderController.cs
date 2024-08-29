using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json.Linq;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.Speechmatics;
using SmartTalk.Messages.Requests.PhoneOrder;

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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderRecordsResponse))]
    public async Task<IActionResult> GetPhoneOrderRecordsAsync([FromQuery] GetPhoneOrderRecordsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderRecordsRequest, GetPhoneOrderRecordsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("conversations"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderConversationsResponse))]
    public async Task<IActionResult> GetPhoneOrderConversationsAsync([FromQuery] GetPhoneOrderConversationsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderConversationsRequest, GetPhoneOrderConversationsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("items"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderOrderItemsRessponse))]
    public async Task<IActionResult> GetPhoneOrderOrderItemsAsync([FromQuery] GetPhoneOrderOrderItemsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderOrderItemsRequest, GetPhoneOrderOrderItemsRessponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("conversation/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddPhoneOrderConversationsResponse))]
    public async Task<IActionResult> AddPhoneOrderConversationsAsync([FromBody] AddPhoneOrderConversationsCommand command) 
    {
        var response = await _mediator.SendAsync<AddPhoneOrderConversationsCommand, AddPhoneOrderConversationsResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }

    [HttpPost("record/receive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceivePhoneOrderRecordAsync([FromForm] IFormFile file)
    {
        var ms = new MemoryStream();

        await file.CopyToAsync(ms).ConfigureAwait(false);

        var fileContent = ms.ToArray();
        
        await _mediator.SendAsync(new ReceivePhoneOrderRecordCommand { RecordName = file.FileName, RecordContent = fileContent }).ConfigureAwait(false);
        
        return Ok();
    }

    [HttpPost("transcription/callback")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TranscriptionCallbackResponse))]
    public async Task<IActionResult> TranscriptionCallback(JObject jObject)
    {
        var transcription = jObject.ToObject<SpeechmaticsGetTranscriptionResponseDto>();
        
        var response = await _mediator.SendAsync<TranscriptionCallbackCommand, TranscriptionCallbackResponse>(new TranscriptionCallbackCommand{Transcription = transcription});
        
        return Ok(response);
    }
}