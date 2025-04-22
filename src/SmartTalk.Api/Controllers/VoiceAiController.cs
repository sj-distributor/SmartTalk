using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VoiceAiController : ControllerBase
{
    private readonly IMediator _mediator;

    public VoiceAiController(IMediator mediator)
    {
        _mediator = mediator;
    }
}