using Mediator.Net;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.Linphone;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
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
    
    [Route("record/scenario"), HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdatePhoneOrderRecordResponse))]
    public async Task<IActionResult> UpdatePhoneOrderRecordAsync([FromBody] UpdatePhoneOrderRecordCommand command)
    {
        var response = await _mediator.SendAsync<UpdatePhoneOrderRecordCommand, UpdatePhoneOrderRecordResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("record/scenario/history"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderRecordScenarioResponse))]
    public async Task<IActionResult> GetPhoneOrderRecordScenarioAsync([FromQuery] GetPhoneOrderRecordScenarioRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderRecordScenarioRequest, GetPhoneOrderRecordScenarioResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("conversations"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderConversationsResponse))]
    public async Task<IActionResult> GetPhoneOrderConversationsAsync([FromQuery] GetPhoneOrderConversationsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderConversationsRequest, GetPhoneOrderConversationsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("conversations/{restaurant}/{id}"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderConversationsResponse))]
    public async Task<IActionResult> GetPhoneOrderConversationsAsync(int restaurant, int id)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderConversationsRequest, GetPhoneOrderConversationsResponse>(new GetPhoneOrderConversationsRequest { RecordId = id }).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("items"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderOrderItemsResponse))]
    public async Task<IActionResult> GetPhoneOrderOrderItemsAsync([FromQuery] GetPhoneOrderOrderItemsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderOrderItemsRequest, GetPhoneOrderOrderItemsResponse>(request).ConfigureAwait(false);
        
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
        
        await _mediator.SendAsync(new ReceivePhoneOrderRecordCommand { 
            RecordName = file.FileName, 
            RecordContent = fileContent, 
            AgentId = agentId, 
            OrderRecordType = PhoneOrderRecordType.InBound}).ConfigureAwait(false);
        
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
    
    [Route("record/report"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderRecordReportResponse))]
    public async Task<IActionResult> GetPhoneOrderRecordReportAsync([FromQuery] GetPhoneOrderRecordReportRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderRecordReportRequest, GetPhoneOrderRecordReportResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }

    [Route("data/dashboard"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderDataDashboardResponse))]
    public async Task<IActionResult> GetPhoneOrderDataDashboardAsync([FromQuery] GetPhoneOrderDataDashboardRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderDataDashboardRequest, GetPhoneOrderDataDashboardResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("tasks"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderRecordTasksResponse))]
    public async Task<IActionResult> GetPhoneOrderRecordTasksAsync([FromQuery] GetPhoneOrderRecordTasksRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderRecordTasksRequest, GetPhoneOrderRecordTasksResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("tasks/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdatePhoneOrderRecordTasksResponse))]
    public async Task<IActionResult> UpdatePhoneOrderRecordTasksAsync([FromBody] UpdatePhoneOrderRecordTasksCommand request)
    {
        var response = await _mediator.SendAsync<UpdatePhoneOrderRecordTasksCommand, UpdatePhoneOrderRecordTasksResponse>(request).ConfigureAwait(false);
        
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
    
    [AllowAnonymous]
    [Route("linphone/get"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetLinphoneHistoryResponse))]
    public async Task<IActionResult> GetLinphoneHistoryAsync([FromQuery] GetLinphoneHistoryRequest request)
    {
        var response = await _mediator.RequestAsync<GetLinphoneHistoryRequest, GetLinphoneHistoryResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }
    
    [AllowAnonymous]
    [Route("linphone/agent"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetLinphoneHistoryResponse))]
    public async Task<IActionResult> GetAgentBySipAsync([FromQuery] GetAgentBySipRequest request)
    {
        var response = await _mediator.RequestAsync<GetAgentBySipRequest, GetAgentBySipResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }
    
    [AllowAnonymous]
    [Route("linphone/details"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetLinphoneHistoryDetailsResponse))]
    public async Task<IActionResult> GetLinphoneHistoryDetailsAsync([FromQuery] GetLinphoneHistoryDetailsRequest request)
    {
        var response = await _mediator.RequestAsync<GetLinphoneHistoryDetailsRequest, GetLinphoneHistoryDetailsResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    [AllowAnonymous]
    [Route("linphone/restaurant"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetLinphoneRestaurantNumberResponse))]
    public async Task<IActionResult> GetLinphoneRestaurantNumberAsync([FromQuery] GetLinphoneRestaurantNumberRequest query)
    {
        var response = await _mediator.RequestAsync<GetLinphoneRestaurantNumberRequest, GetLinphoneRestaurantNumberResponse>(query).ConfigureAwait(false);
        
        return Ok(response);
    }

    [Route("linphone/data"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetLinphoneDataResponse))]
    public async Task<IActionResult> GetLinphoneDataAsync([FromQuery] GetLinphoneDataRequest request)
    {
        var response = await _mediator.RequestAsync<GetLinphoneDataRequest, GetLinphoneDataResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    #endregion
}