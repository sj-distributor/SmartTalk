using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.PhoneCall;

namespace SmartTalk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PhoneCallController : ControllerBase
{
    private readonly IMediator _mediator;

    public PhoneCallController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("assistant")]
    [HttpPost("assistant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CallAiSpeechAssistantAsync([FromForm] CallAiSpeechAssistantCommand command)
    {
        await _mediator.SendAsync(command);

        return Ok();
    }

    [HttpGet("assistant/connect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task ConnectAiSpeechAssistantAsync([FromQuery] ConnectAiSpeechAssistantCommand command)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            command.TwilioWebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await _mediator.SendAsync(command);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
}