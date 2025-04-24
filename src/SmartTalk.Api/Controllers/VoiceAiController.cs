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
        
    [Route("pos/companies"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosCompanyWithStoresResponse))]
    public async Task<IActionResult> GetPosCompanyWithStoresAsync([FromQuery] GetPosCompanyWithStoresRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosCompanyWithStoresRequest, GetPosCompanyWithStoresResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("pos/company/store/detail"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosCompanyStoreDetailResponse))]
    public async Task<IActionResult> GetPosCompanyStoreDetailAsync([FromQuery] GetPosCompanyStoreDetailRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosCompanyStoreDetailRequest, GetPosCompanyStoreDetailResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("pos/company/store/create"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreatePosCompanyStoreCommand))]
    public async Task<IActionResult> CreatePosCompanyStoreAsync([FromBody] CreatePosCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<CreatePosCompanyStoreCommand, CreatePosCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("pos/company/store/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdatePosCompanyStoreCommand))]
    public async Task<IActionResult> UpdatePosCompanyStoreAsync([FromBody] UpdatePosCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<UpdatePosCompanyStoreCommand, UpdatePosCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("pos/company/store/delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeletePosCompanyStoreCommand))]
    public async Task<IActionResult> DeletePosCompanyStoreAsync([FromBody] DeletePosCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<DeletePosCompanyStoreCommand, DeletePosCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("pos/store/status/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdatePosCompanyStoreStatusResponse))]
    public async Task<IActionResult> UpdatePosCompanyStoreStatusAsync([FromBody] UpdatePosCompanyStoreStatusCommand command)
    {
        var response = await _mediator.SendAsync<UpdatePosCompanyStoreStatusCommand, UpdatePosCompanyStoreStatusResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("pos/store/unbind"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UnbindPosCompanyStoreResponse))]
    public async Task<IActionResult> UnbindPosCompanyStoreAsync([FromBody] UnbindPosCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<UnbindPosCompanyStoreCommand, UnbindPosCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
}