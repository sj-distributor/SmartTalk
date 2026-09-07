using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Requests.Notification;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CallNotificationController : ControllerBase
{
    private readonly IMediator _mediator;

    public CallNotificationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 获取通话通知场景规则及指定门店的通知开关配置。
    /// </summary>
    [Route("scenarios"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CallNotificationScenarioRulesResponse))]
    public async Task<IActionResult> GetScenarioRulesAsync([FromQuery] GetCallNotificationScenarioRulesRequest request)
    {
        var response = await _mediator.RequestAsync<GetCallNotificationScenarioRulesRequest, CallNotificationScenarioRulesResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    /// <summary>
    /// 新增或更新通话通知场景规则。
    /// 由运营用户维护场景说明、AI Prompt 模板及规则启用状态。
    /// </summary>
    [Route("scenarios"), HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CallNotificationOperationResponse))]
    public async Task<IActionResult> UpsertScenarioRuleAsync([FromBody] UpsertCallNotificationScenarioRuleRequest request)
    {
        var response = await _mediator.RequestAsync<UpsertCallNotificationScenarioRuleRequest, CallNotificationOperationResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    /// <summary>
    /// 更新指定门店的通话场景通知开关。
    /// </summary>
    [Route("settings"), HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CallNotificationOperationResponse))]
    public async Task<IActionResult> UpdateStoreSettingAsync([FromBody] UpdateStoreCallNotificationSettingRequest request)
    {
        var response = await _mediator.RequestAsync<UpdateStoreCallNotificationSettingRequest, CallNotificationOperationResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    /// <summary>
    /// 分页查询通话通知发送记录。
    /// 支持按公司、门店、场景、状态和客户号码筛选。
    /// </summary>
    [Route("records"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CallNotificationRecordsResponse))]
    public async Task<IActionResult> GetRecordsAsync([FromQuery] GetCallNotificationRecordsRequest request)
    {
        var response = await _mediator.RequestAsync<GetCallNotificationRecordsRequest, CallNotificationRecordsResponse>(request).ConfigureAwait(false);

        return Ok(response);
    }

    /// <summary>
    /// 按筛选条件导出通话通知记录。
    /// 导出当前用户有权限查看的全部匹配记录。
    /// </summary>
    [Route("records/export"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CallNotificationExportResponse))]
    public async Task<IActionResult> ExportRecordsAsync([FromQuery] GetCallNotificationRecordsRequest request)
    {
        var exportRequest = new ExportCallNotificationRecordsRequest
        {
            CompanyId = request.CompanyId, StoreId = request.StoreId, ScenarioKey = request.ScenarioKey,
            Status = request.Status, CustomerNumberKeyword = request.CustomerNumberKeyword,
            PageIndex = request.PageIndex, PageSize = request.PageSize
        };
        var response = await _mediator.RequestAsync<ExportCallNotificationRecordsRequest, CallNotificationExportResponse>(exportRequest).ConfigureAwait(false);

        return Ok(response);
    }
}
