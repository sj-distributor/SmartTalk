using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
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
    [Produces("application/sdp")]
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

        var result = await _registry.CreateAsync(
            assistantId,
            region,
            offerSdp,
            cancellationToken).ConfigureAwait(false);

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Append("X-Realtime-Call-Id", result.CallId);
        return Content(result.AnswerSdp, "application/sdp");
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
