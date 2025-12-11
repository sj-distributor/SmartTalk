using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.Hr;
using SmartTalk.Messages.Requests.Hr;
using Microsoft.AspNetCore.Authorization;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class HrController : ControllerBase
{
    private readonly IMediator _mediator;

    public HrController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    #region interview_question
    
    [Route("interview/questions"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetCurrentInterviewQuestionsResponse))]
    public async Task<IActionResult> GetCurrentInterviewQuestionsAsync([FromQuery] GetCurrentInterviewQuestionsRequest request)
    {
        var response = await _mediator.RequestAsync<GetCurrentInterviewQuestionsRequest, GetCurrentInterviewQuestionsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("interview/questions"), HttpPost]
    public async Task<IActionResult> AddHrInterviewQuestionsAsync([FromBody] AddHrInterviewQuestionsCommand command)
    {
        await _mediator.SendAsync(command);

        return Ok();
    }
    
    #endregion
}