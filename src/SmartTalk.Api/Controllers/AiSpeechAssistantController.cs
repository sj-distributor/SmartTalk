using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiSpeechAssistantController : ControllerBase
{
    private readonly IMediator _mediator;

    public AiSpeechAssistantController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Route("call"), HttpGet, HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CallAiSpeechAssistantAsync([FromForm] CallAiSpeechAssistantCommand command)
    {
        command.Host = HttpContext.Request.Host.Host;
        var response = await _mediator.SendAsync<CallAiSpeechAssistantCommand, CallAiSpeechAssistantResponse>(command);

        return response.Data;
    }
    
    [HttpGet("connect")]
    [HttpGet("connect/{from}/{to}")]
    public async Task ConnectAiSpeechAssistantAsync(string from, string to)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var command = new ConnectAiSpeechAssistantCommand
            {
                From = from,
                To = to,
                TwilioWebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync()
            };
            await _mediator.SendAsync(command);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
}