using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using SmartTalk.Core.Services.Twilio;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Commands.Twilio;
using SmartTalk.Messages.Requests.Twilio;
using Twilio.Security;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TwilioWebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISmsOptOutService _smsOptOutService;
    private readonly TwilioSettings _twilioSettings;

    public TwilioWebhookController(IMediator mediator, ISmsOptOutService smsOptOutService, TwilioSettings twilioSettings)
    {
        _mediator = mediator;
        _smsOptOutService = smsOptOutService;
        _twilioSettings = twilioSettings;
    }
    
    [Route("phonecall"), HttpPost]
    public async Task<IActionResult> HandlePhoneCallStatusCallBackAsync([FromBody] HandlePhoneCallStatusCallBackCommand command)
    {
        await _mediator.SendAsync(command).ConfigureAwait(false);
        
        return Ok();
    }

    [AllowAnonymous]
    [Route("messages/inbound"), HttpPost]
    public async Task<IActionResult> HandleIncomingMessageAsync([FromForm] IncomingTwilioMessageRequest request, CancellationToken cancellationToken)
    {
        if (!await IsValidTwilioRequestAsync().ConfigureAwait(false))
            return Unauthorized();

        if (!string.Equals(request.Body?.Trim(), "STOP", StringComparison.OrdinalIgnoreCase))
            return Ok();

        await _smsOptOutService.OptOutByInboundMessageAsync(request.From, request.To, cancellationToken).ConfigureAwait(false);

        return Ok();
    }

    private async Task<bool> IsValidTwilioRequestAsync()
    {
        if (string.IsNullOrWhiteSpace(_twilioSettings.IncomingMessageWebhookUrl))
        {
            Log.Error("Twilio incoming-message webhook rejected because Twilio:IncomingMessageWebhookUrl is not configured.");
            return false;
        }

        var signature = Request.Headers["X-Twilio-Signature"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(signature))
            return false;

        var form = await Request.ReadFormAsync().ConfigureAwait(false);
        var parameters = form.ToDictionary(x => x.Key, x => x.Value.ToString());

        return new RequestValidator(_twilioSettings.AuthToken).Validate(
            _twilioSettings.IncomingMessageWebhookUrl,
            parameters,
            signature);
    }
}
