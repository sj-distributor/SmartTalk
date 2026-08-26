using Mediator.Net;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using SmartTalk.Api.Authentication.TemporarySession;
using SmartTalk.Api.Extensions;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.RealtimeAiWebRtc;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Api.Controllers;

[ApiController]
[EnableCors(CorsPolicyExtension.RealtimeAiWebRtcPocPolicy)]
[Route("api/[controller]")]
public sealed class RealtimeAiWebRtcController : ControllerBase
{
    private const string InvalidSessionMessage = "The interview session is invalid or has expired.";
    private const string RecordingSequenceHeader = "X-Recording-Sequence";
    private const string RecordingFinalHeader = "X-Recording-Final";
    private const int MaxSdpLength = 64 * 1024;
    private const int MaxRecordingChunkLength = 256 * 1024;

    private readonly IMediator _mediator;
    private readonly IAiSpeechAssistantSessionCredentialService _sessionCredentialService;

    public RealtimeAiWebRtcController(
        IMediator mediator,
        IAiSpeechAssistantSessionCredentialService sessionCredentialService)
    {
        _mediator = mediator;
        _sessionCredentialService = sessionCredentialService;
    }

    [HttpPost("session/{assistantId:int}/{region}")]
    [TemporarySessionAuthorize]
    [Consumes("application/sdp", "text/plain")]
    [Produces("application/json")]
    public async Task<IActionResult> CreateSessionAsync(
        int assistantId,
        RealtimeAiServerRegion region,
        CancellationToken cancellationToken)
    {
        if (!TryGetTemporarySessionId(out var sessionId))
            return InvalidSession();

        if (Request.ContentLength > MaxSdpLength)
            return BadRequest(new { error = $"SDP offer exceeds {MaxSdpLength} bytes." });

        using var reader = new StreamReader(Request.Body);
        var offerSdp = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(offerSdp) || offerSdp.Length > MaxSdpLength)
            return BadRequest(new { error = "Invalid SDP offer." });

        CreateRealtimeAiWebRtcSessionResponse response;
        try
        {
            response = await _mediator.SendAsync<
                CreateRealtimeAiWebRtcSessionCommand,
                CreateRealtimeAiWebRtcSessionResponse>(new CreateRealtimeAiWebRtcSessionCommand
                {
                    SessionId = sessionId,
                    AssistantId = assistantId,
                    Region = region,
                    OfferSdp = offerSdp
                }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "[RealtimeAiWebRtc] Failed to create session, AssistantId: {AssistantId}, Region: {Region}",
                assistantId,
                region);

            // The application's global exception filter converts exceptions to HTTP 200.
            // Keep this diagnostic endpoint honest so the static client can surface the cause.
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }

        if (response.IsSessionAlreadyBound)
            return Conflict(new { error = "The interview session is already bound to a WebRTC call." });

        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            callId = response.CallId,
            sdp = response.AnswerSdp
        });
    }

    [HttpPost("session/{callId}/ready")]
    [TemporarySessionAuthorize]
    public async Task<IActionResult> MarkClientReadyAsync(string callId, CancellationToken cancellationToken)
    {
        if (!TryGetTemporarySessionId(out var sessionId))
            return InvalidSession();

        if (!await _sessionCredentialService
                .IsWebRtcCallBoundAsync(sessionId, callId, cancellationToken)
                .ConfigureAwait(false))
            return NotFound();

        var response = await _mediator.SendAsync<
            MarkRealtimeAiWebRtcClientReadyCommand,
            MarkRealtimeAiWebRtcClientReadyResponse>(new MarkRealtimeAiWebRtcClientReadyCommand
            {
                CallId = callId
            }, cancellationToken).ConfigureAwait(false);

        return response.IsFound
            ? NoContent()
            : NotFound();
    }

    [HttpPost("session/{callId}/recording")]
    [TemporarySessionAuthorize]
    [Consumes("application/octet-stream")]
    public async Task<IActionResult> AppendRecordingAsync(
        string callId,
        [FromHeader(Name = RecordingSequenceHeader)] long? sequence,
        [FromHeader(Name = RecordingFinalHeader)] bool isFinal,
        CancellationToken cancellationToken)
    {
        if (!TryGetTemporarySessionId(out var sessionId))
            return InvalidSession();

        if (!await _sessionCredentialService
                .IsWebRtcCallBoundAsync(sessionId, callId, cancellationToken)
                .ConfigureAwait(false))
            return NotFound();

        if (sequence is null or < 0)
            return BadRequest(new { error = $"{RecordingSequenceHeader} must be a non-negative integer." });

        if (Request.ContentLength is > MaxRecordingChunkLength)
            return BadRequest(new { error = $"Recording chunk exceeds {MaxRecordingChunkLength} bytes." });

        var pcmBytes = new byte[MaxRecordingChunkLength + 1];
        var length = 0;
        while (length < pcmBytes.Length)
        {
            var read = await Request.Body
                .ReadAsync(pcmBytes.AsMemory(length, pcmBytes.Length - length), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) break;
            length += read;
        }

        if ((!isFinal && length == 0) || length > MaxRecordingChunkLength || length % sizeof(short) != 0)
            return BadRequest(new { error = "Recording chunk must contain 24 kHz mono PCM16LE audio." });

        Array.Resize(ref pcmBytes, length);
        var response = await _mediator.SendAsync<
            AppendRealtimeAiWebRtcRecordingCommand,
            AppendRealtimeAiWebRtcRecordingResponse>(new AppendRealtimeAiWebRtcRecordingCommand
            {
                CallId = callId,
                Sequence = sequence.Value,
                IsFinal = isFinal,
                PcmBytes = pcmBytes
            }, cancellationToken).ConfigureAwait(false);

        return response.Status switch
        {
            RealtimeAiWebRtcRecordingAppendStatus.Accepted or
                RealtimeAiWebRtcRecordingAppendStatus.Duplicate => NoContent(),
            RealtimeAiWebRtcRecordingAppendStatus.NotFound => NotFound(),
            RealtimeAiWebRtcRecordingAppendStatus.InvalidSequence => Conflict(new
            {
                error = "Recording chunk sequence is not the next expected sequence.",
                nextSequence = response.NextSequence
            }),
            RealtimeAiWebRtcRecordingAppendStatus.RecordingLimitExceeded => StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new { error = "Recording exceeds the maximum session size." }),
            RealtimeAiWebRtcRecordingAppendStatus.RateLimitExceeded => StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { error = "Recording chunks are arriving faster than real-time audio." }),
            _ => Conflict(new { error = "Recording has already been finalized." })
        };
    }

    [HttpDelete("session/{callId}")]
    [TemporarySessionAuthorize]
    public async Task<IActionResult> StopSessionAsync(string callId, CancellationToken cancellationToken)
    {
        if (!TryGetTemporarySessionId(out var sessionId))
            return InvalidSession();

        if (!await _sessionCredentialService
                .IsWebRtcCallBoundAsync(sessionId, callId, cancellationToken)
                .ConfigureAwait(false))
            return NotFound();

        try
        {
            var response = await _mediator.SendAsync<
                StopRealtimeAiWebRtcSessionCommand,
                StopRealtimeAiWebRtcSessionResponse>(new StopRealtimeAiWebRtcSessionCommand
                {
                    CallId = callId
                }, cancellationToken).ConfigureAwait(false);

            return response.IsFound
                ? NoContent()
                : NotFound();
        }
        finally
        {
            await _sessionCredentialService
                .InvalidateAsync(sessionId, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private bool TryGetTemporarySessionId(out Guid sessionId)
    {
        var rawSessionId = User.FindFirst(TemporarySessionAuthenticationDefaults.SessionIdClaim)?.Value;
        return Guid.TryParse(rawSessionId, out sessionId);
    }

    private IActionResult InvalidSession()
    {
        return Unauthorized(new { code = StatusCodes.Status401Unauthorized, msg = InvalidSessionMessage });
    }
}
