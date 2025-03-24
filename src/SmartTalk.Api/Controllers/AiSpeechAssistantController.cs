using Serilog;
using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AiSpeechAssistantController : ControllerBase
{
    private readonly IMediator _mediator;

    public AiSpeechAssistantController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [AllowAnonymous]
    [Route("call"), HttpGet, HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CallAiSpeechAssistantAsync([FromForm] CallAiSpeechAssistantCommand command)
    {
        command.Host = HttpContext.Request.Host.Host;
        var response = await _mediator.SendAsync<CallAiSpeechAssistantCommand, CallAiSpeechAssistantResponse>(command);

        return response.Data;
    }

    [AllowAnonymous]
    [HttpGet("connect/{from}/{to}")]
    public async Task ConnectAiSpeechAssistantAsync(string from, string to)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var command = new ConnectAiSpeechAssistantCommand
            {
                From = from,
                To = to,
                Host = HttpContext.Request.Host.Host,
                TwilioWebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync()
            };
            await _mediator.SendAsync(command);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
    
    [AllowAnonymous]
    [HttpGet("outbound/connect")]
    [HttpGet("outbound/connect/{from}/{to}/{id}")]
    public async Task OutboundConnectAiSpeechAssistantAsync(string from, string to, int id)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var command = new ConnectAiSpeechAssistantCommand
            {
                From = from,
                To = to,
                AssistantId = id,
                Host = HttpContext.Request.Host.Host,
                TwilioWebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync()
            };
            await _mediator.SendAsync(command);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    [AllowAnonymous]
    [Route("recording/callback"), HttpPost]
    public async Task<IActionResult> ReceivePhoneRecordingStatusCallBackAcyn()
    {
        using (var reader = new StreamReader(Request.Body))
        {
            var body = await reader.ReadToEndAsync();
            
            var query = QueryHelpers.ParseQuery(body);

            Log.Information("Receiving Phone call recording callback command, sid: {CallSid} 、status: {RecordingStatus}、url: {url}", query["CallSid"], query["RecordingStatus"], query["RecordingUrl"]);
            
            await _mediator.SendAsync(new ReceivePhoneRecordingStatusCallbackCommand
            {
                CallSid = query["CallSid"],
                RecordingSid = query["RecordingSid"],
                RecordingUrl = query["RecordingUrl"],
                RecordingTrack = query["RecordingTrack"],
                RecordingStatus = query["RecordingStatus"]
            }).ConfigureAwait(false);
        }
        
        return Ok();
    }
    
    [Route("numbers"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetNumbersResponse))]
    public async Task<IActionResult> GetNumbersAsync([FromQuery] GetNumbersRequest request)
    {
        var response = await _mediator.RequestAsync<GetNumbersRequest, GetNumbersResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("assistant"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAiSpeechAssistantByIdResponse))]
    public async Task<IActionResult> GetAssistantByIdAsync([FromBody] GetAiSpeechAssistantByIdRequest command)
    {
        var response = await _mediator.RequestAsync<GetAiSpeechAssistantByIdRequest, GetAiSpeechAssistantByIdResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("assistants"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAiSpeechAssistantsResponse))]
    public async Task<IActionResult> GetAiSpeechAssistantsAsync([FromQuery] GetAiSpeechAssistantsRequest request)
    {
        var response = await _mediator.RequestAsync<GetAiSpeechAssistantsRequest, GetAiSpeechAssistantsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("knowledge"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAiSpeechAssistantKnowledgeResponse))]
    public async Task<IActionResult> GetGetAiSpeechAssistantKnowledgeAsync([FromQuery] GetAiSpeechAssistantKnowledgeRequest request)
    {
        var response = await _mediator.RequestAsync<GetAiSpeechAssistantKnowledgeRequest, GetAiSpeechAssistantKnowledgeResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("knowledge/history"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAiSpeechAssistantKnowledgeHistoryResponse))]
    public async Task<IActionResult> GetGetAiSpeechAssistantKnowledgeHistoryAsync([FromQuery] GetAiSpeechAssistantKnowledgeHistoryRequest request)
    {
        var response = await _mediator.RequestAsync<GetAiSpeechAssistantKnowledgeHistoryRequest, GetAiSpeechAssistantKnowledgeHistoryResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("assistant/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddAiSpeechAssistantResponse))]
    public async Task<IActionResult> AddAiSpeechAssistantAsync([FromBody] AddAiSpeechAssistantCommand command)
    {
        var response = await _mediator.SendAsync<AddAiSpeechAssistantCommand, AddAiSpeechAssistantResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("knowledge/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddAiSpeechAssistantKnowledgeResponse))]
    public async Task<IActionResult> AddAiSpeechAssistantKnowledgeAsync([FromBody] AddAiSpeechAssistantKnowledgeCommand command)
    {
        var response = await _mediator.SendAsync<AddAiSpeechAssistantKnowledgeCommand, AddAiSpeechAssistantKnowledgeResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("knowledge/switch"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwitchAiSpeechAssistantKnowledgeVersionResponse))]
    public async Task<IActionResult> SwitchAiSpeechAssistantKnowledgeVersionAsync([FromBody] SwitchAiSpeechAssistantKnowledgeVersionCommand command)
    {
        var response = await _mediator.SendAsync<SwitchAiSpeechAssistantKnowledgeVersionCommand, SwitchAiSpeechAssistantKnowledgeVersionResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("assistant/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateAiSpeechAssistantResponse))]
    public async Task<IActionResult> UpdateAiSpeechAssistantAsync([FromBody] UpdateAiSpeechAssistantCommand command)
    {
        var response = await _mediator.SendAsync<UpdateAiSpeechAssistantCommand, UpdateAiSpeechAssistantResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("assistant/delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteAiSpeechAssistantResponse))]
    public async Task<IActionResult> DeleteAiSpeechAssistantAsync([FromBody] DeleteAiSpeechAssistantCommand command)
    {
        var response = await _mediator.SendAsync<DeleteAiSpeechAssistantCommand, DeleteAiSpeechAssistantResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("assistant/number/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateAiSpeechAssistantNumberResponse))]
    public async Task<IActionResult> UpdateAiSpeechAssistantNumberAsync([FromBody] UpdateAiSpeechAssistantNumberCommand command)
    {
        var response = await _mediator.SendAsync<UpdateAiSpeechAssistantNumberCommand, UpdateAiSpeechAssistantNumberResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
}