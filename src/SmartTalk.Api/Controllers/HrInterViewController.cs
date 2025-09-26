using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.HrInterView;
using SmartTalk.Messages.Requests.HrInterView;
using SmartTalk.Core.Services.HrInterView;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class HrInterViewController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHrInterViewService _hrInterViewService;

    public HrInterViewController(IMediator mediator, IHrInterViewService hrInterViewService)
    {
        _mediator = mediator;
        _hrInterViewService = hrInterViewService;
    }
    
        
    [Route("setting"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetHrInterViewSettingsResponse))]
    public async Task<IActionResult> GetHrInterViewSettingsAsync([FromQuery] GetHrInterViewSettingsRequest request)
    {
        var response = await _mediator.RequestAsync<GetHrInterViewSettingsRequest, GetHrInterViewSettingsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("setting/addOrUpdate"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddOrUpdateHrInterViewSettingAsync([FromBody] AddOrUpdateHrInterViewSettingCommand command, CancellationToken cancellationToken)
    {
        command.Host = HttpContext.Request.Host.Host;
        
        var response = await _mediator.SendAsync<AddOrUpdateHrInterViewSettingCommand, AddOrUpdateHrInterViewSettingResponse>(command, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }
    
        
    [Route("session"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetHrInterViewSessionsResponse))]
    public async Task<IActionResult> GetHrInterViewSessionsAsync([FromQuery] GetHrInterViewSessionsRequest request)
    {
        var response = await _mediator.RequestAsync<GetHrInterViewSessionsRequest, GetHrInterViewSessionsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [AllowAnonymous]
    [HttpGet("websocket/{sessionId}")]
    public async Task ConnectHrInterViewAsync(Guid sessionId)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var command = new ConnectHrInterViewCommand
            {
                Host = HttpContext.Request.Host.Host,
                SessionId = sessionId,
                WebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync()
            };
            
            await _mediator.SendAsync(command);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
}