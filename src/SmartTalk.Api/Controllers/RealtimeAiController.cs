using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using SmartTalk.Messages.Commands.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RealtimeAiController : ControllerBase
{
    private readonly IMediator _mediator;

    public RealtimeAiController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [AllowAnonymous]
    [HttpGet("connect/{assistantId}")]
    public async Task RealtimeAiConnectAsync(int assistantId)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var requested = HttpContext.WebSockets.WebSocketRequestedProtocols;
            
            if (!Enum.TryParse<RealtimeAiAudioCodec>(requested.FirstOrDefault(x => x.StartsWith("InputFormat."))?.Replace("InputFormat.", ""), ignoreCase: true, out var inputFormat))
            {
                HttpContext.Response.StatusCode = 400;
                await HttpContext.Response.WriteAsync("Invalid InputFormat enum value");
                return;
            }

            if (!Enum.TryParse<RealtimeAiAudioCodec>(requested.FirstOrDefault(x => x.StartsWith("OutputFormat."))?.Replace("OutputFormat.", ""), ignoreCase: true, out var outputFormat))
            {
                HttpContext.Response.StatusCode = 400;
                await HttpContext.Response.WriteAsync("Invalid OutputFormat enum value");
                return;
            }
            
            Log.Information("Realtime Api AssistantId: {AssistantId}, InputFormat: {RealtimeAiAudioCodec}, OutputFormat: {OutputFormat}", assistantId, inputFormat, outputFormat);
            
            var command = new RealtimeAiConnectCommand
            {
                AssistantId = assistantId,
                InputFormat = inputFormat,
                OutputFormat = outputFormat,
                WebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync()
            };
            
            await _mediator.SendAsync(command).ConfigureAwait(false);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
}