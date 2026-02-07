using Serilog;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using Task = System.Threading.Tasks.Task;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task TransferHumanServiceAsync(TransferHumanServiceCommand command, CancellationToken cancellationToken);

    Task HangupCallAsync(string callSid, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task TransferHumanServiceAsync(TransferHumanServiceCommand command, CancellationToken cancellationToken)
    {
        Log.Information("Transfer human service command");

        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

        var call = await CallResource.UpdateAsync(
            pathSid: command.CallSid,
            twiml: $"<Response>\n    <Dial>\n      <Number>{command.HumanPhone}</Number>\n    </Dial>\n  </Response>"
        );
    }

    public async Task HangupCallAsync(string callSid, CancellationToken cancellationToken)
    {
        if (_aiSpeechAssistantStreamContext.IsTransfer) return;

        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

        await CallResource.UpdateAsync(
            pathSid: callSid,
            status: CallResource.UpdateStatusEnum.Completed
        );
    }
}