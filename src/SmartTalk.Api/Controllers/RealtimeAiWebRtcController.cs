using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using SmartTalk.Api.Extensions;
using SmartTalk.Core.Services.RealtimeAiWebRtc;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Api.Controllers;

[AllowAnonymous]
[ApiController]
[EnableCors(CorsPolicyExtension.RealtimeAiWebRtcPocPolicy)]
[Route("api/[controller]")]
public sealed class RealtimeAiWebRtcController : ControllerBase
{
    private const int MaxSdpLength = 64 * 1024;

    private readonly IRealtimeAiWebRtcSessionRegistry _registry;

    public RealtimeAiWebRtcController(IRealtimeAiWebRtcSessionRegistry registry)
    {
        _registry = registry;
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

        RealtimeAiWebRtcCallResult result;
        try
        {
            result = await _registry.CreateAsync(
                assistantId,
                region,
                offerSdp,
                cancellationToken).ConfigureAwait(false);
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
            callId = result.CallId,
            sdp = result.AnswerSdp
        });
    }

    [HttpPost("session/{callId}/ready")]
    public async Task<IActionResult> MarkClientReadyAsync(string callId)
    {
        return await _registry.MarkClientReadyAsync(callId).ConfigureAwait(false)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("session/{callId}")]
    public async Task<IActionResult> StopSessionAsync(string callId)
    {
        return await _registry.StopAsync(callId).ConfigureAwait(false)
            ? NoContent()
            : NotFound();
    }
}
