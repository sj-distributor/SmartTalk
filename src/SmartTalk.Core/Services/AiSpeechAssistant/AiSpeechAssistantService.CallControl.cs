using Serilog;
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

        await _twilioService.UpdateCallTwimlAsync(
            command.CallSid,
            $"<Response>\n    <Dial>\n      <Number>{command.HumanPhone}</Number>\n    </Dial>\n  </Response>");
    }

    public async Task HangupCallAsync(string callSid, CancellationToken cancellationToken)
    {
        if (_aiSpeechAssistantStreamContext.IsTransfer) return;

        await _twilioService.CompleteCallAsync(callSid);
    }
}
