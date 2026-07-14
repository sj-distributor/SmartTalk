using System.Linq.Expressions;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private RealtimeAiFunctionCallResult ProcessHangup(CancellationToken cancellationToken)
    {
        _ = cancellationToken;  // intentionally unused — Hangfire-deferred jobs must not capture scope tokens

        _backgroundJobClient.Schedule(BuildHangupJobExpression(_ctx.CallSid), TimeSpan.FromSeconds(2));

        return new RealtimeAiFunctionCallResult
        {
            Output = "Say goodbye to the guests in their **language**"
        };
    }

    /// <summary>
    /// Builds the Hangfire job expression that defers the hangup by N seconds.
    /// <para>
    /// Capturing the request-scope <c>CancellationToken</c> here is unsafe: by the time
    /// Hangfire fires the job (≥2s later), the originating request scope is already
    /// disposed and the captured CTS would throw <c>ObjectDisposedException</c>. Always
    /// pass <see cref="CancellationToken.None"/>; long-running cleanup work in the
    /// scheduled handler is responsible for its own lifecycle.
    /// </para>
    /// <para>Public static for unit testability; not intended for external use.</para>
    /// </summary>
    public static Expression<Func<IAiSpeechAssistantService, Task>> BuildHangupJobExpression(string callSid) =>
        service => service.HangupCallAsync(callSid, CancellationToken.None);
}
