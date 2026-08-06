using Serilog;
using Serilog.Context;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Logging;
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

        // Ambient for the whole session. Everything the engine calls into inherits it — including the
        // provider WSS client's receive loop, which Task.Run starts inside this scope — so those log
        // lines become filterable by call without editing any of their call sites.
        using var callScope = LogContext.Push(new DeferredLogScope().Set(LogProperties.RealtimeSessionId, _ctx.SessionId));

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

    private string GetWebSocketStateSafe()
    {
        try { return _ctx.WebSocket?.State.ToString() ?? "null"; }
        catch { return "unknown"; }
    }
}
