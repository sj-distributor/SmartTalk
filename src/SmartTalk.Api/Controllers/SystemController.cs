using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly IMediator _mediator;

    public SystemController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [Route("usages"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneCallUsagesPreviewResponse))]
    public async Task<IActionResult> GetPhoneCallUsagesPreviewAsync([FromQuery] GetPhoneCallUsagesPreviewRequest command)
    {
        var response = await _mediator.RequestAsync<GetPhoneCallUsagesPreviewRequest, GetPhoneCallUsagesPreviewResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
        
    [Route("usages/detail"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneCallRecordDetailResponse))]
    public async Task<IActionResult> GetPhoneCallrecordDetailAsync([FromQuery] GetPhoneCallRecordDetailRequest command)
    {
        var response = await _mediator.RequestAsync<GetPhoneCallRecordDetailRequest, GetPhoneCallRecordDetailResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("company/report"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderCompanyCallReportResponse))]
    public async Task<IActionResult> GetPhoneOrderCompanyCallReportAsync([FromQuery] GetPhoneOrderCompanyCallReportRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderCompanyCallReportRequest, GetPhoneOrderCompanyCallReportResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    [Route("external/inbound/redirect"), HttpPost]
    public async Task<IActionResult> ConfigureAiSpeechAssistantInboundRouteAsync([FromBody] ConfigureAiSpeechAssistantInboundRouteCommand command)
    {
        await _mediator.SendAsync(command).ConfigureAwait(false);
        
        return Ok();
    }
    
    [AllowAnonymous]
    [Route("test"), HttpGet]
    public async Task<IActionResult> TestAsync()
    {
        return Ok("成功！！");
    }
}