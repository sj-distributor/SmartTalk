using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PosController(IMediator mediator)
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
    
    [Route("company/check"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CheckPosCompanyOrStoreResponse))]
    public async Task<IActionResult> CheckPosCompanyAsync([FromQuery] CheckPosCompanyOrStoreRequest request)
    {
        var response = await _mediator.RequestAsync<CheckPosCompanyOrStoreRequest, CheckPosCompanyOrStoreResponse>(request).ConfigureAwait(false);
        
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
    public async Task<IActionResult> GetPosCompanyDetailAsync([FromQuery] GetPosCompanyDetailRequest request)
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

    [Route("configuration/sync"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SyncPosConfigurationResponse))]
    public async Task<IActionResult> SyncPosConfigurationAsync([FromBody] SyncPosConfigurationCommand command)
    {
        var response = await _mediator.SendAsync<SyncPosConfigurationCommand, SyncPosConfigurationResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("store/unbind"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UnbindPosCompanyStoreResponse))]
    public async Task<IActionResult> UnbindPosCompanyStoreAsync([FromBody] UnbindPosCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<UnbindPosCompanyStoreCommand, UnbindPosCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("store/bind"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BindPosCompanyStoreResponse))]
    public async Task<IActionResult> BindPosCompanyStoreAsync([FromBody] BindPosCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<BindPosCompanyStoreCommand, BindPosCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/menu/detail"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosMenuDetailResponse))]
    public async Task<IActionResult> GetPosMenuDetailAsync([FromQuery] GetPosMenuDetailRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosMenuDetailRequest, GetPosMenuDetailResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/menu/preview"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosMenuPreviewResponse))]
    public async Task<IActionResult> GetPosMenuPreviewAsync([FromQuery] GetPosMenuPreviewRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosMenuPreviewRequest, GetPosMenuPreviewResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/menus"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosMenusListResponse))]
    public async Task<IActionResult> GetPosMenusListAsync([FromQuery] GetPosMenusListRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosMenusListRequest, GetPosMenusListResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/menu/category"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosCategoryResponse))]
    public async Task<IActionResult> GetPosCategoryAsync([FromQuery] GetPosCategoryRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosCategoryRequest, GetPosCategoryResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/menu/categories"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosCategoriesResponse))]
    public async Task<IActionResult> GetPosCategoriesAsync([FromQuery] GetPosCategoriesRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosCategoriesRequest, GetPosCategoriesResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/menu/product"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosProductResponse))]
    public async Task<IActionResult> GetPosProductAsync([FromQuery] GetPosProductRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosProductRequest, GetPosProductResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/menu/products"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosProductsResponse))]
    public async Task<IActionResult> GetPosProductsAsync([FromQuery] GetPosProductsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosProductsRequest, GetPosProductsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/menu/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdatePosMenuResponse))]
    public async Task<IActionResult> UpdatePosMenuAsync([FromBody] UpdatePosMenuCommand command)
    {
        var response = await _mediator.SendAsync<UpdatePosMenuCommand, UpdatePosMenuResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/menu/category/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdatePosCategoryResponse))]
    public async Task<IActionResult> UpdatePosCategoryAsync([FromBody] UpdatePosCategoryCommand command)
    {
        var response = await _mediator.SendAsync<UpdatePosCategoryCommand, UpdatePosCategoryResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/menu/product/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdatePosProductResponse))]
    public async Task<IActionResult> UpdatePosProductAsync([FromBody] UpdatePosProductCommand command)
    {
        var response = await _mediator.SendAsync<UpdatePosProductCommand, UpdatePosProductResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("stores"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosStoresResponse))]
    public async Task<IActionResult> GetPosStoresAsync([FromQuery] GetPosStoresRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosStoresRequest, GetPosStoresResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("orders"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosStoreOrdersResponse))]
    public async Task<IActionResult> GetPosStoreOrdersAsync([FromQuery] GetPosStoreOrdersRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosStoreOrdersRequest, GetPosStoreOrdersResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("order"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosStoreOrderResponse))]
    public async Task<IActionResult> GetPosStoreOrderAsync([FromQuery] GetPosStoreOrderRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosStoreOrderRequest, GetPosStoreOrderResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("order/products"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosOrderProductsResponse))]
    public async Task<IActionResult> GetPosOrderProductsAsync([FromQuery] GetPosOrderProductsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosOrderProductsRequest, GetPosOrderProductsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("order/place"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlacePosOrderResponse))]
    public async Task<IActionResult> PlacePosOrderAsync([FromBody] PlacePosOrderCommand command)
    {
        var response = await _mediator.SendAsync<PlacePosOrderCommand, PlacePosOrderResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("menu/sort"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdjustPosMenuContentSortResponse))]
    public async Task<IActionResult> AdjustPosMenuContentSortAsync([FromBody] AdjustPosMenuContentSortCommand command)
    {
        var response = await _mediator.SendAsync<AdjustPosMenuContentSortCommand, AdjustPosMenuContentSortResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("order/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePosOrderAsync([FromBody] UpdatePosOrderCommand command)
    {
        await _mediator.SendAsync(command).ConfigureAwait(false);
        
        return Ok();
    }
    
    [Route("customer/info/match"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosCustomerInfoResponse))]
    public async Task<IActionResult> GetPosCustomerInfosAsync([FromQuery] GetPosCustomerInfoRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosCustomerInfoRequest, GetPosCustomerInfoResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
}