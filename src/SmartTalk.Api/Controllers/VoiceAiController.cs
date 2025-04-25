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
    
    [Route("pos/store/account/bind"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BindPosCompanyStoreAccountsResponse))]
    public async Task<IActionResult> BindPosCompanyStoreAccountsAsync([FromBody] BindPosCompanyStoreAccountsCommand command)
    {
        var response = await _mediator.SendAsync<BindPosCompanyStoreAccountsCommand, BindPosCompanyStoreAccountsResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
}