using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Timer;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public interface IRealtimeAiService : IScopedDependency
{
    Task ConnectAsync(RealtimeSessionOptions options, CancellationToken cancellationToken);
}

public partial class RealtimeAiService : IRealtimeAiService
{
    private RealtimeAiSessionContext _ctx;

    private readonly IRealtimeAiSwitcher _realtimeAiSwitcher;
    private readonly IInactivityTimerManager _inactivityTimerManager;

    public RealtimeAiService(
        IRealtimeAiSwitcher realtimeAiSwitcher,
        IInactivityTimerManager inactivityTimerManager)
    {
        _realtimeAiSwitcher = realtimeAiSwitcher;
        _inactivityTimerManager = inactivityTimerManager;
    }

    public async Task ConnectAsync(RealtimeSessionOptions options, CancellationToken cancellationToken)
    {
        BuildSessionContext(options, cancellationToken);

        Log.Information("[RealtimeAi] Session initialized, Context: {@Context}", _ctx);

        try
        {
            await ConnectToProviderAsync().ConfigureAwait(false);
        }
        catch
        {
            await DisconnectFromProviderAsync("Session start failed").ConfigureAwait(false);
            throw;
        }

        await OrchestrateSessionAsync().ConfigureAwait(false);
    }

    private void StartInactivityTimer(int seconds, string followUpMessage)
    {
        _inactivityTimerManager.StartTimer(_ctx.StreamSid, TimeSpan.FromSeconds(seconds), async () =>
        {
            Log.Information("[RealtimeAi] Idle follow-up triggered, SessionId: {SessionId}, TimeoutSeconds: {TimeoutSeconds}",
                _ctx.SessionId, seconds);

            await SendTextToProviderAsync(followUpMessage);
        });
    }

    private void StopInactivityTimer()
    {
        _inactivityTimerManager.StopTimer(_ctx.StreamSid);
    }

    private string GetWebSocketStateSafe()
    {
        try { return _ctx.WebSocket?.State.ToString() ?? "null"; }
        catch { return "unknown"; }
    }
}
