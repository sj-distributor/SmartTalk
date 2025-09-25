using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.HrInterView;
using SmartTalk.Messages.Requests.HrInterView;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class HrInterViewController : ControllerBase
{
    private readonly IMediator _mediator;

    public HrInterViewController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
        
    [Route("setting"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetHrInterViewSettingsResponse))]
    public async Task<IActionResult> GetHrInterViewSettingsAsync([FromQuery] GetHrInterViewSettingsRequest request)
    {
        var response = await _mediator.RequestAsync<GetHrInterViewSettingsRequest, GetHrInterViewSettingsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("setting/addOrUpdate"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddOrUpdateHrInterViewSettingAsync([FromBody] AddOrUpdateHrInterViewSettingCommand command, CancellationToken cancellationToken)
    {
        var response = await _mediator.SendAsync<AddOrUpdateHrInterViewSettingCommand, AddOrUpdateHrInterViewSettingResponse>(command, cancellationToken).ConfigureAwait(false);

        return Ok(response);
    }
}