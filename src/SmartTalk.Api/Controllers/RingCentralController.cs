using Microsoft.AspNetCore.Mvc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.RingCentral;

namespace SmartTalk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RingCentralController : ControllerBase
{
    private readonly RingCentralClient _ringCentralClient;

    public RingCentralController(RingCentralClient ringCentralClient)
    {
        _ringCentralClient = ringCentralClient;
    }

    /// <summary>
    /// 获取 RingCentral OAuth Token
    /// </summary>
    [HttpPost("token")]
    public async Task<ActionResult<RingCentralTokenResponseDto>> GetToken(CancellationToken cancellationToken)
    {
        var result = await _ringCentralClient.TokenAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}