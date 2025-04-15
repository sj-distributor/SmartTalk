using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.Restaurants;
using SmartTalk.Messages.Requests.Restaurant;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RestaurantController : ControllerBase
{
    private readonly IMediator _mediator;

    public RestaurantController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Route("create"), HttpPost]
    public async Task<IActionResult> AddRestaurantAsync([FromBody] AddRestaurantCommand command)
    {
        await _mediator.SendAsync(command).ConfigureAwait(false);
        
        return Ok();
    }
    
    [Route("menuItems"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetRestaurantMenuItemsResponse))]
    public async Task<IActionResult> GetRestaurantMenuItemsAsync([FromQuery] GetRestaurantMenuItemsRequest request)
    {
        var response = await _mediator.RequestAsync<GetRestaurantMenuItemsRequest, GetRestaurantMenuItemsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("modifierProducts/Prompt"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    public async Task<IActionResult> GetModifierProductsPromptAsync([FromQuery] GetModifierProductsPromptRequest request, CancellationToken cancellationToken)
    { 
        var response = await _mediator.RequestAsync<GetModifierProductsPromptRequest, GetModifierProductsPromptResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
}