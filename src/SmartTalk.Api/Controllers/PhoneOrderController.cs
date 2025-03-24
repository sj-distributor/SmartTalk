using Serilog;
using Mediator.Net;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.Linphone;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Requests.Linphone;
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
    public async Task<IActionResult> ReceivePhoneOrderRecordAsync([FromForm] IFormFile file, [FromForm] int agentId)
    {
        var ms = new MemoryStream();

        await file.CopyToAsync(ms).ConfigureAwait(false);

        var fileContent = ms.ToArray();
        
        await _mediator.SendAsync(new ReceivePhoneOrderRecordCommand { RecordName = file.FileName, RecordContent = fileContent, AgentId = agentId }).ConfigureAwait(false);
        
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
    
    [Route("order/place"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaceOrderAndModifyItemResponse))]
    public async Task<IActionResult> PlaceOrderAndModifyItemsAsync([FromBody] PlaceOrderAndModifyItemCommand command)
    {
        var response = await _mediator.SendAsync<PlaceOrderAndModifyItemCommand, PlaceOrderAndModifyItemResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }

    #region Linphone
    
    [Route("linphone/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddLinphoneCdrAsync([FromBody] AddLinphoneCdrCommand command)
    {
        await _mediator.SendAsync(command).ConfigureAwait(false);

        return Ok();
    }
    
    [Route("linphone/get"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetLinphoneHistoryResponse))]
    public async Task<IActionResult> GetLinphoneHistoryAsync([FromQuery] GetLinphoneHistoryRequest request)
    {
        var response = await _mediator.RequestAsync<GetLinphoneHistoryRequest, GetLinphoneHistoryResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("linphone/agent"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetLinphoneHistoryResponse))]
    public async Task<IActionResult> GetAgentBySipAsync([FromQuery] GetAgentBySipRequest request)
    {
        var response = await _mediator.RequestAsync<GetAgentBySipRequest, GetAgentBySipResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("linphone/details"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetLinphoneHistoryDetailsResponse))]
    public async Task<IActionResult> GetLinphoneHistoryDetailsAsync([FromQuery] GetLinphoneHistoryDetailsRequest request)
    {
        var response = await _mediator.RequestAsync<GetLinphoneHistoryDetailsRequest, GetLinphoneHistoryDetailsResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }
    
    #endregion
}