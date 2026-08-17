using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using SmartTalk.Api.Extensions;
using SmartTalk.Messages.Commands.RealtimeAiWebRtc;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Api.Controllers;

[AllowAnonymous]
[ApiController]
[EnableCors(CorsPolicyExtension.RealtimeAiWebRtcPocPolicy)]
[Route("api/[controller]")]
public sealed class RealtimeAiWebRtcController : ControllerBase
{
    private const int MaxSdpLength = 64 * 1024;

    private readonly IMediator _mediator;

    public RealtimeAiWebRtcController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("session/{assistantId:int}/{region}")]
    [Consumes("application/sdp", "text/plain")]
    [Produces("application/json")]
    public async Task<IActionResult> CreateSessionAsync(
        int assistantId,
        RealtimeAiServerRegion region,
        CancellationToken cancellationToken)
    {
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

        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            callId = response.CallId,
            sdp = response.AnswerSdp
        });
    }

    [HttpPost("session/{callId}/ready")]
    public async Task<IActionResult> MarkClientReadyAsync(string callId, CancellationToken cancellationToken)
    {
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

    [HttpDelete("session/{callId}")]
    public async Task<IActionResult> StopSessionAsync(string callId, CancellationToken cancellationToken)
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
}
