using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

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
    
    [Route("company/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreatePosCompanyResponse))]
    public async Task<IActionResult> CreatePosCompanyAsync([FromBody] CreatePosCompanyCommand command)
    {
        var response = await _mediator.SendAsync<CreatePosCompanyCommand, CreatePosCompanyResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdatePosCompanyResponse))]
    public async Task<IActionResult> UpdatePosCompanyAsync([FromBody] UpdatePosCompanyCommand command)
    {
        var response = await _mediator.SendAsync<UpdatePosCompanyCommand, UpdatePosCompanyResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeletePosCompanyResponse))]
    public async Task<IActionResult> DeletePosCompanyAsync([FromBody] DeletePosCompanyCommand command)
    {
        var response = await _mediator.SendAsync<DeletePosCompanyCommand, DeletePosCompanyResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/update/status"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdatePosCompanyStatusResponse))]
    public async Task<IActionResult> UpdatePosCompanyStatusAsync([FromBody] UpdatePosCompanyStatusCommand command)
    {
        var response = await _mediator.SendAsync<UpdatePosCompanyStatusCommand, UpdatePosCompanyStatusResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/detail"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosCompanyDetailResponse))]
    public async Task<IActionResult> GetPosCompanyDetailRequest([FromQuery] GetPosCompanyDetailRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosCompanyDetailRequest, GetPosCompanyDetailResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
}