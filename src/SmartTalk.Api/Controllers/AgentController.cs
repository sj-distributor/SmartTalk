using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Requests.Agent;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAiSpeechAssistantProcessJobService _aiSpeechAssistantProcessJobService;

    public AgentController(IMediator mediator, IAiSpeechAssistantProcessJobService aiSpeechAssistantProcessJobService)
    {
        _mediator = mediator;
        _aiSpeechAssistantProcessJobService = aiSpeechAssistantProcessJobService;
    }
    
    [Route("agents"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAgentsResponse))]
    public async Task<IActionResult> GetAgentsAsync([FromQuery] GetAgentsRequest request)
    {
        var response = await _mediator.RequestAsync<GetAgentsRequest, GetAgentsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("Yesy"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Test()
    {
        await _aiSpeechAssistantProcessJobService.OpenAiAccountTrainingAsync(new OpenAiAccountTrainingCommand(), CancellationToken.None).ConfigureAwait(false);
        
        
        return Ok();
    }
}