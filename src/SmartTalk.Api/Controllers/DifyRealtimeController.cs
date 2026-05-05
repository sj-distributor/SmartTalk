using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Core.Services.DifyRealtime;
using SmartTalk.Messages.Dto.DifyRealtime;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DifyRealtimeController : ControllerBase
{
    private readonly IDifyRealtimeBridgeService _bridgeService;

    public DifyRealtimeController(IDifyRealtimeBridgeService bridgeService)
    {
        _bridgeService = bridgeService;
    }

    [AllowAnonymous]
    [HttpPost("message")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DifyRealtimeMessageResponse))]
    public async Task<IActionResult> SendMessageAsync([FromBody] DifyRealtimeMessageRequest request, CancellationToken cancellationToken)
    {
        var response = await _bridgeService.SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("end")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DifyRealtimeEndSessionResponse))]
    public async Task<IActionResult> EndSessionAsync([FromBody] DifyRealtimeEndSessionRequest request, CancellationToken cancellationToken)
    {
        var response = await _bridgeService.EndSessionAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(response);
    }
}
