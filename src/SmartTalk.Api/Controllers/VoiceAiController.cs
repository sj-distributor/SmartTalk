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
        
    [Route("companies"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosCompanyWithStoresResponse))]
    public async Task<IActionResult> GetPosCompanyWithStoresAsync([FromQuery] GetPosCompanyWithStoresRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosCompanyWithStoresRequest, GetPosCompanyWithStoresResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/store/detail"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosCompanyStoreDetailResponse))]
    public async Task<IActionResult> GetPosCompanyStoreDetailAsync([FromQuery] GetPosCompanyStoreDetailRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosCompanyStoreDetailRequest, GetPosCompanyStoreDetailResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/store/create"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreatePosCompanyStoreCommand))]
    public async Task<IActionResult> CreatePosCompanyStoreAsync([FromBody] CreatePosCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<CreatePosCompanyStoreCommand, CreatePosCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/store/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdatePosCompanyStoreCommand))]
    public async Task<IActionResult> UpdatePosCompanyStoreAsync([FromBody] UpdatePosCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<UpdatePosCompanyStoreCommand, UpdatePosCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/store/delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeletePosCompanyStoreCommand))]
    public async Task<IActionResult> DeletePosCompanyStoreAsync([FromBody] DeletePosCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<DeletePosCompanyStoreCommand, DeletePosCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("store/status/update"), HttpPost]
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
    
    [Route("store/account/manage"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ManagePosCompanyStoreAccountsResponse))]
    public async Task<IActionResult> ManagePosCompanyStoreAccountsAsync([FromBody] ManagePosCompanyStoreAccountsCommand command)
    {
        var response = await _mediator.SendAsync<ManagePosCompanyStoreAccountsCommand, ManagePosCompanyStoreAccountsResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("store/accounts"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosStoreUsersResponse))]
    public async Task<IActionResult> GetPosStoreUsersAsync([FromQuery] GetPosStoreUsersRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosStoreUsersRequest, GetPosStoreUsersResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }

    [Route("pos/configuration/sync"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SyncPosConfigurationResponse))]
    public async Task<IActionResult> SyncPosConfigurationAsync([FromBody] SyncPosConfigurationCommand command)
    {
        var response = await _mediator.SendAsync<SyncPosConfigurationCommand, SyncPosConfigurationResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("pos/store/unbind"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UnbindPosCompanyStoreResponse))]
    public async Task<IActionResult> UnbindPosCompanyStoreAsync([FromBody] UnbindPosCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<UnbindPosCompanyStoreCommand, UnbindPosCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("pos/store/bind"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BindPosCompanyStoreResponse))]
    public async Task<IActionResult> BindPosCompanyStoreAsync([FromBody] BindPosCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<BindPosCompanyStoreCommand, BindPosCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
}