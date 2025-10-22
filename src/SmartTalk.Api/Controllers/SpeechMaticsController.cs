using Serilog;
using Mediator.Net;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Commands.SpeechMatics;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SpeechMaticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SpeechMaticsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("callback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TranscriptionCallbackAsync(JObject jObject)
    {
        Log.Information("Receive Speech matics callback : {@callback}", jObject);
        
        await _mediator.SendAsync(new DistributeSpeechMaticsCallbackCommand { CallBackMessage = jObject.ToString() }).ConfigureAwait(false);

        return Ok();
    }
}