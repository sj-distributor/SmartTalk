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

    [Route("assistant")]
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> AiSpeechAssistantCallAsync(IFormCollection form)
    {
        await _mediator.SendAsync<AiSpeechAssistantCallCommand>(new AiSpeechAssistantCallCommand { From = form["From"], To = form["To"]} );

        return Ok();
    }
}