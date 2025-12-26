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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetCompanyWithStoresResponse))]
    public async Task<IActionResult> GetPosCompanyWithStoresAsync([FromQuery] GetCompanyWithStoresRequest request)
    {
        var response = await _mediator.RequestAsync<GetCompanyWithStoresRequest, GetCompanyWithStoresResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/store/detail"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetCompanyStoreDetailResponse))]
    public async Task<IActionResult> GetPosCompanyStoreDetailAsync([FromQuery] GetCompanyStoreDetailRequest request)
    {
        var response = await _mediator.RequestAsync<GetCompanyStoreDetailRequest, GetCompanyStoreDetailResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/store/create"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateCompanyStoreCommand))]
    public async Task<IActionResult> CreatePosCompanyStoreAsync([FromBody] CreateCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<CreateCompanyStoreCommand, CreateCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/store/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateCompanyStoreCommand))]
    public async Task<IActionResult> UpdatePosCompanyStoreAsync([FromBody] UpdateCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<UpdateCompanyStoreCommand, UpdateCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/store/delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteCompanyStoreCommand))]
    public async Task<IActionResult> DeletePosCompanyStoreAsync([FromBody] DeleteCompanyStoreCommand command)
    {
        var response = await _mediator.SendAsync<DeleteCompanyStoreCommand, DeleteCompanyStoreResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("store/status/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateCompanyStoreStatusResponse))]
    public async Task<IActionResult> UpdatePosCompanyStoreStatusAsync([FromBody] UpdateCompanyStoreStatusCommand command)
    {
        var response = await _mediator.SendAsync<UpdateCompanyStoreStatusCommand, UpdateCompanyStoreStatusResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateCompanyResponse))]
    public async Task<IActionResult> CreatePosCompanyAsync([FromBody] CreateCompanyCommand command)
    {
        var response = await _mediator.SendAsync<CreateCompanyCommand, CreateCompanyResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/update"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateCompanyResponse))]
    public async Task<IActionResult> UpdatePosCompanyAsync([FromBody] UpdateCompanyCommand command)
    {
        var response = await _mediator.SendAsync<UpdateCompanyCommand, UpdateCompanyResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/check"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CheckCompanyOrStoreResponse))]
    public async Task<IActionResult> CheckPosCompanyAsync([FromQuery] CheckCompanyOrStoreRequest request)
    {
        var response = await _mediator.RequestAsync<CheckCompanyOrStoreRequest, CheckCompanyOrStoreResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/delete"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeleteCompanyResponse))]
    public async Task<IActionResult> DeletePosCompanyAsync([FromBody] DeleteCompanyCommand command)
    {
        var response = await _mediator.SendAsync<DeleteCompanyCommand, DeleteCompanyResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("company/update/status"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateCompanyStatusResponse))]
    public async Task<IActionResult> UpdatePosCompanyStatusAsync([FromBody] UpdateCompanyStatusCommand command)
    {
        var response = await _mediator.SendAsync<UpdateCompanyStatusCommand, UpdateCompanyStatusResponse>(command).ConfigureAwait(false);
        
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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ManageCompanyStoreAccountsResponse))]
    public async Task<IActionResult> ManagePosCompanyStoreAccountsAsync([FromBody] ManageCompanyStoreAccountsCommand command)
    {
        var response = await _mediator.SendAsync<ManageCompanyStoreAccountsCommand, ManageCompanyStoreAccountsResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("store/accounts"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetStoreUsersResponse))]
    public async Task<IActionResult> GetPosStoreUsersAsync([FromQuery] GetStoreUsersRequest request)
    {
        var response = await _mediator.RequestAsync<GetStoreUsersRequest, GetStoreUsersResponse>(request).ConfigureAwait(false);
        
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
    public async Task<IActionResult> GetPosStoresAsync([FromQuery] GetStoresRequest request)
    {
        var response = await _mediator.RequestAsync<GetStoresRequest, GetPosStoresResponse>(request).ConfigureAwait(false);
        
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
    
    [Route("order/cloudPrintStatus"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPosOrderCloudPrintStatusResponse))]
    public async Task<IActionResult> GetPosOrderProductsAsync([FromQuery] GetPosOrderCloudPrintStatusRequest request)
    {
        var response = await _mediator.RequestAsync<GetPosOrderCloudPrintStatusRequest, GetPosOrderCloudPrintStatusResponse>(request).ConfigureAwait(false);
        
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
    
    [Route("customers"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetStoreCustomersResponse))]
    public async Task<IActionResult> GetPosCustomerInfosAsync([FromQuery] GetStoreCustomersRequest request)
    {
        var response = await _mediator.RequestAsync<GetStoreCustomersRequest, GetStoreCustomersResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("customer"), HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateStoreCustomerResponse))]
    public async Task<IActionResult> UpdateStoreCustomerAsync([FromBody] UpdateStoreCustomerCommand command)
    {
        var response = await _mediator.SendAsync<UpdateStoreCustomerCommand, UpdateStoreCustomerResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("store/agents"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetCurrentUserStoresResponse))]
    public async Task<IActionResult> GetPosAgentsAsync([FromQuery] GetCurrentUserStoresRequest request)
    {
        var response = await _mediator.RequestAsync<GetCurrentUserStoresRequest, GetCurrentUserStoresResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("stores/agents"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetStoresAgentsResponse))]
    public async Task<IActionResult> GetAgentsStoresAsync([FromQuery] GetStoresAgentsRequest request)
    {
        var response = await _mediator.RequestAsync<GetStoresAgentsRequest, GetStoresAgentsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("all/stores"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetAllStoresResponse))]
    public async Task<IActionResult> GetAllStoresAsync([FromQuery] GetAllStoresRequest request)
    {
        var response = await _mediator.RequestAsync<GetAllStoresRequest, GetAllStoresResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("simple/stores"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetSimpleStructuredStoresResponse))]
    public async Task<IActionResult> GetSimpleStructuredStoresAsync([FromQuery] GetSimpleStructuredStoresRequest request)
    {
        var response = await _mediator.RequestAsync<GetSimpleStructuredStoresRequest, GetSimpleStructuredStoresResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("get/printStatus"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPrintStatusResponse))]
    public async Task<IActionResult> GetPrintStatusAsync([FromQuery] GetPrintStatusRequest request)
    {
        var response = await _mediator.RequestAsync<GetPrintStatusRequest, GetPrintStatusResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("update/printStatus"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdatePosOrderPrintStatusResponse))]
    public async Task<IActionResult> UpdatePosOrderPrintStatusAsync([FromBody] UpdatePosOrderPrintStatusCommand request)
    {
        var response = await _mediator.SendAsync<UpdatePosOrderPrintStatusCommand, UpdatePosOrderPrintStatusResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("data/dashboard/companies"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetCompanyWithStoresResponse))]
    public async Task<IActionResult> GetDataDashBoardCompanyWithStoresAsync([FromQuery] GetDataDashBoardCompanyWithStoresRequest request)
    {
        var response = await _mediator.RequestAsync<GetDataDashBoardCompanyWithStoresRequest, GetDataDashBoardCompanyWithStoresResponse>(request).ConfigureAwait(false);
     
        return Ok(response);
    }

    [Route("get/reservationInfo"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetOrderReservationInfoResponse))]
    public async Task<IActionResult> GetOrderReservationInfoAsync([FromQuery] GetOrderReservationInfoRequest request)
    {
        var response = await _mediator.RequestAsync<GetOrderReservationInfoRequest, GetOrderReservationInfoResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("update/reservationInfo"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateOrderReservationInfoResponse))]
    public async Task<IActionResult> UpdateOrderReservationInfoAsync([FromBody] UpdateOrderReservationInfoCommand command)
    {
        var response = await _mediator.SendAsync<UpdateOrderReservationInfoCommand, UpdateOrderReservationInfoResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }
}