using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using SmartTalk.Messages.Commands.RealtimeHttp;
using SmartTalk.Messages.Dto.RealtimeHttp;
using SmartTalk.Messages.Requests.RealtimeHttp;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RealtimeHttpGatewayController : ControllerBase
{
    private readonly IMediator _mediator;

    public RealtimeHttpGatewayController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [HttpPost("sessions")]
    public async Task<ActionResult<RealtimeHttpCreateSessionResponse>> CreateSessionAsync(
        [FromBody] RealtimeHttpCreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateRealtimeHttpSessionCommand
            {
                AssistantId = request.AssistantId,
                Region = request.Region
            };

            var result = await _mediator.SendAsync<CreateRealtimeHttpSessionCommand, RealtimeHttpCreateSessionResponse>(command, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<RealtimeHttpSessionDetailResponse>>> ListSessions()
    {
        var request = new GetRealtimeHttpSessionsRequest();
        var response = await _mediator.RequestAsync<GetRealtimeHttpSessionsRequest, GetRealtimeHttpSessionsResponse>(request).ConfigureAwait(false);

        return Ok(response.Sessions);
    }
    
    [HttpPost("scripted/default-two-turns")]
    public async Task<ActionResult<RealtimeHttpRunDefaultConversationResponse>> RunDefaultTwoTurnsAsync(
        [FromBody] RealtimeHttpRunDefaultConversationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator
                .SendAsync<RealtimeHttpRunDefaultConversationRequest, RealtimeHttpRunDefaultConversationResponse>(request, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [HttpGet("sessions/{sessionId}")]
    public async Task<ActionResult<RealtimeHttpSessionDetailResponse>> GetSessionAsync(string sessionId)
    {
        var request = new GetRealtimeHttpSessionRequest
        {
            SessionId = sessionId
        };
        var response = await _mediator.RequestAsync<GetRealtimeHttpSessionRequest, GetRealtimeHttpSessionResponse>(request).ConfigureAwait(false);
        if (!response.Found) return NotFound(new { message = "session_not_found" });

        return Ok(response.Session);
    }
    
    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<ActionResult<RealtimeHttpSendMessageResponse>> SendMessageAsync(
        string sessionId,
        [FromBody] RealtimeHttpSendMessageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new SendRealtimeHttpSessionMessageCommand
            {
                SessionId = sessionId,
                Text = request.Text,
                TimeoutMs = request.TimeoutMs
            };

            var result = await _mediator.SendAsync<SendRealtimeHttpSessionMessageCommand, RealtimeHttpSendMessageResponse>(command, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [HttpDelete("sessions/{sessionId}")]
    public async Task<ActionResult<RealtimeHttpDisconnectResponse>> DisconnectSessionAsync(
        string sessionId,
        [FromQuery] string reason = "http_client_disconnect",
        CancellationToken cancellationToken = default)
    {
        var command = new DisconnectRealtimeHttpSessionCommand
        {
            SessionId = sessionId,
            Reason = reason
        };
        var result = await _mediator.SendAsync<DisconnectRealtimeHttpSessionCommand, RealtimeHttpDisconnectResponse>(command, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Closed) return NotFound(result);

        return Ok(result);
    }
    
    [HttpGet("recordings/{sessionId}")]
    public async Task<ActionResult<RealtimeHttpRecordingInfoResponse>> GetRecordingAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new GetRealtimeHttpRecordingInfoRequest
            {
                SessionIdOrProviderSessionId = sessionId
            };

            var result = await _mediator.RequestAsync<GetRealtimeHttpRecordingInfoRequest, RealtimeHttpRecordingInfoResponse>(request, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [HttpGet("recordings/{sessionId}/file")]
    public async Task<IActionResult> DownloadRecordingAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var request = new GetRealtimeHttpRecordingInfoRequest
        {
            SessionIdOrProviderSessionId = sessionId
        };
        var info = await _mediator.RequestAsync<GetRealtimeHttpRecordingInfoRequest, RealtimeHttpRecordingInfoResponse>(request, cancellationToken)
            .ConfigureAwait(false);
        if (!info.Ready || string.IsNullOrWhiteSpace(info.RecordingPath) || !System.IO.File.Exists(info.RecordingPath))
            return NotFound(info);

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(info.RecordingPath, out var contentType))
            contentType = "application/octet-stream";

        var downloadName = string.IsNullOrWhiteSpace(info.RecordingFileName)
            ? $"{(string.IsNullOrWhiteSpace(info.ProviderSessionId) ? sessionId : info.ProviderSessionId)}.wav"
            : info.RecordingFileName;

        return PhysicalFile(info.RecordingPath, contentType, downloadName, enableRangeProcessing: true);
    }
}
