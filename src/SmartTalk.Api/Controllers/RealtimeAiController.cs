using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.AiKids;
using SmartTalk.Messages.Enums.PhoneOrder;
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
    public async Task ConnectAiKidRealtimeAsync(int assistantId)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var command = new AiKidRealtimeCommand
            {
                AssistantId = assistantId,
                InputFormat = RealtimeAiAudioCodec.PCM16,
                OutputFormat = RealtimeAiAudioCodec.PCM16,
                WebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync(),
                Region = RealtimeAiServerRegion.US,
                OrderRecordType = PhoneOrderRecordType.TestLink
            };
            
            await _mediator.SendAsync(command).ConfigureAwait(false);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
    
    [AllowAnonymous]
    [HttpGet("connect/{assistantId}/{region}")]
    public async Task HkConnectAiKidRealtimeAsync(int assistantId, RealtimeAiServerRegion region)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var command = new AiKidRealtimeCommand
            {
                AssistantId = assistantId,
                InputFormat = RealtimeAiAudioCodec.PCM16,
                OutputFormat = RealtimeAiAudioCodec.PCM16,
                WebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync(),
                Region = region,
                OrderRecordType = PhoneOrderRecordType.TestLink
            };
            
            await _mediator.SendAsync(command).ConfigureAwait(false);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
}