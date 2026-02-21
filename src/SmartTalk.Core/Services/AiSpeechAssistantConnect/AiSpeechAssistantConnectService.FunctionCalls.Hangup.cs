using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private RealtimeAiFunctionCallResult ProcessHangup(CancellationToken cancellationToken)
    {
        _backgroundJobClient.Schedule<IAiSpeechAssistantService>(
            x => x.HangupCallAsync(_ctx.CallSid, cancellationToken), TimeSpan.FromSeconds(2));

        return new RealtimeAiFunctionCallResult
        {
            Output = "Say goodbye to the guests in their **language**"
        };
    }
}
