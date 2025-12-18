using System.Net.WebSockets;
using Serilog;
using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.RealtimeAi.Services;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AiSpeechAssistantController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly IRealtimeAiService _realtimeAiService;

    public AiSpeechAssistantController(IMediator mediator, IConfiguration configuration, IRealtimeAiService realtimeAiService)
    {
        _mediator = mediator;
        _configuration = configuration;
        _realtimeAiService = realtimeAiService;
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
    [HttpGet("outbound/connect/{from}/{to}/{id}/{numberId?}")]
    public async Task OutboundConnectAiSpeechAssistantAsync(string from, string to, int id, int? numberId = null)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            Log.Information("Outbound connect to assistant, from: {From}, to: {To}, assistantId: {AssistantId}, greeting: {numberId}", from, to, id, numberId);
            var command = new ConnectAiSpeechAssistantCommand
            {
                From = from,
                To = to,
                AssistantId = id,
                Host = HttpContext.Request.Host.Host,
                NumberId = numberId,
                TwilioWebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync(),
                OrderRecordType = PhoneOrderRecordType.OutBount,
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
    public async Task<IActionResult> GetAiSpeechAssistantByIdAsync([FromQuery] GetAiSpeechAssistantByIdRequest request)
    {
        var response = await _mediator.RequestAsync<GetAiSpeechAssistantByIdRequest, GetAiSpeechAssistantByIdResponse>(request).ConfigureAwait(false);

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
    
    [Route("assistant/detail"), HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateAiSpeechAssistantDetailResponse))]
    public async Task<IActionResult> UpdateAiSpeechAssistantDetailAsync([FromBody] UpdateAiSpeechAssistantDetailCommand command)
    {
        var response = await _mediator.SendAsync<UpdateAiSpeechAssistantDetailCommand, UpdateAiSpeechAssistantDetailResponse>(command).ConfigureAwait(false);

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
    
    [Route("knowledge/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateAiSpeechAssistantKnowledgeResponse))]
    public async Task<IActionResult> UpdateAiSpeechAssistantKnowledgeAsync([FromBody] UpdateAiSpeechAssistantKnowledgeCommand command)
    {
        var response = await _mediator.SendAsync<UpdateAiSpeechAssistantKnowledgeCommand, UpdateAiSpeechAssistantKnowledgeResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("realtime/connect"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateRealtimeConnectionResponse))]
    public async Task<IActionResult> CreateRealtimeConnectionAsync([FromBody] CreateRealtimeConnectionCommand command)
    {
        var response = await _mediator.SendAsync<CreateRealtimeConnectionCommand, CreateRealtimeConnectionResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("google/live"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConnectGoogleLiveAsync()
    {
        var url = $"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContent?key={_configuration["Google:ApiKey"]}";

        var clientWebSocket = new ClientWebSocket();
        try
        {
            await clientWebSocket.ConnectAsync(new Uri(url), CancellationToken.None);
            Log.Information("Connect succeeded");
        }
        catch (Exception e)
        {
            Log.Error(e, "Connect failed");
        }
        
        return Ok(clientWebSocket);
    }
    
    [AllowAnonymous]
    [Route("realtime/connect/test"), HttpGet]
    public async Task RealtimeConnectAsync()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            // await _realtimeAiService.RealtimeAiConnectAsync(, assistant, "You are a friendly assistant", RealtimeAiAudioCodec.PCM16, RealtimeAiAudioCodec.PCM16, CancellationToken.None).ConfigureAwait(false);
            await _mediator.SendAsync(new RealtimeAiConnectCommand
            {
                AssistantId = 1,
                WebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync(),
                InputFormat = RealtimeAiAudioCodec.PCM16,
                OutputFormat = RealtimeAiAudioCodec.PCM16
            });
        }else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
    
    [Route("session"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAiSpeechAssistantSessionResponse))]
    public async Task<IActionResult> GetAiSpeechAssistantSessionAsync([FromQuery] GetAiSpeechAssistantSessionRequest request)
    {
        var response = await _mediator.RequestAsync<GetAiSpeechAssistantSessionRequest, GetAiSpeechAssistantSessionResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("session/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddAiSpeechAssistantSessionResponse))]
    public async Task<IActionResult> AddAiSpeechAssistantSessionAsync([FromBody] AddAiSpeechAssistantSessionCommand command)
    {
        var response = await _mediator.SendAsync<AddAiSpeechAssistantSessionCommand, AddAiSpeechAssistantSessionResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("session/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateAiSpeechAssistantSessionResponse))]
    public async Task<IActionResult> UpdateAiSpeechAssistantSessionAsync([FromBody] UpdateAiSpeechAssistantSessionCommand command)
    {
        var response = await _mediator.SendAsync<UpdateAiSpeechAssistantSessionCommand, UpdateAiSpeechAssistantSessionResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("assistant/switch"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwitchAiSpeechDefaultAssistantResponse))]
    public async Task<IActionResult> SwitchAiSpeechDefaultAssistantAsync([FromBody] SwitchAiSpeechDefaultAssistantCommand command)
    {
        var response = await _mediator.SendAsync<SwitchAiSpeechDefaultAssistantCommand, SwitchAiSpeechDefaultAssistantResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("route"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddAiSpeechAssistantInboundRoutesResponse))]
    public async Task<IActionResult> AddAiSpeechAssistantSessionAsync([FromBody] AddAiSpeechAssistantInboundRoutesCommand command)
    {
        var response = await _mediator.SendAsync<AddAiSpeechAssistantInboundRoutesCommand, AddAiSpeechAssistantInboundRoutesResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("route"), HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateAiSpeechAssistantInboundRouteResponse))]
    public async Task<IActionResult> UpdateAiSpeechAssistantSessionAsync([FromBody] UpdateAiSpeechAssistantInboundRouteCommand command)
    {
        var response = await _mediator.SendAsync<UpdateAiSpeechAssistantInboundRouteCommand, UpdateAiSpeechAssistantInboundRouteResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("routes"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteAiSpeechAssistantInboundRoutesResponse))]
    public async Task<IActionResult> DeleteAiSpeechAssistantSessionAsync([FromBody] DeleteAiSpeechAssistantInboundRoutesCommand command)
    {
        var response = await _mediator.SendAsync<DeleteAiSpeechAssistantInboundRoutesCommand, DeleteAiSpeechAssistantInboundRoutesResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("routes"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAiSpeechAssistantInboundRoutesResponse))]
    public async Task<IActionResult> GetGetAiSpeechAssistantKnowledgeAsync([FromQuery] GetAiSpeechAssistantInboundRoutesRequest request)
    {
        var response = await _mediator.RequestAsync<GetAiSpeechAssistantInboundRoutesRequest, GetAiSpeechAssistantInboundRoutesResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
        
    [Route("knowledge/copy"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> KonwledgeCopyAsync([FromBody] KonwledgeCopyCommand command, CancellationToken cancellationToken)
    {
        var response = await _mediator.SendAsync<KonwledgeCopyCommand, KonwledgeCopyResponse>(command, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("knowledges/get"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKonwledgesAsync([FromQuery] GetKonwledgesRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.RequestAsync<GetKonwledgesRequest, GetKonwledgesResponse>(request, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }
    
    [Route("knowledge/realted/get"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKonwledgeRelatedAsync([FromQuery] GetKonwledgeRelatedRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.RequestAsync<GetKonwledgeRelatedRequest, GetKonwledgeRelatedResponse>(request, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }

    #region variable_cache
    
    [Route("caches"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAiSpeechAssistantKnowledgeVariableCacheResponse))]
    public async Task<IActionResult> GetAiSpeechAssistantKnowledgeVariableCacheAsync([FromQuery] GetAiSpeechAssistantKnowledgeVariableCacheRequest request)
    {
        var response = await _mediator.RequestAsync<GetAiSpeechAssistantKnowledgeVariableCacheRequest, GetAiSpeechAssistantKnowledgeVariableCacheResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("caches"), HttpPut]
    public async Task<IActionResult> UpdateAiSpeechAssistantKnowledgeVariableCacheAsync([FromBody] UpdateAiSpeechAssistantKnowledgeVariableCacheCommand command)
    {
        await _mediator.SendAsync(command);

        return Ok();
    }
    
    #endregion
}