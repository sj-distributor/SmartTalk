using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
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
        BuildSessionContext(options);

        Log.Information("[RealtimeAi] Session initialized, Context: {@Context}", _ctx);

        try
        {
            await ConnectToProviderAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisconnectFromProviderAsync("Session start failed").ConfigureAwait(false);
            throw;
        }

        await OrchestrateSessionAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SendToClientAsync(object payload)
    {
        if (_ctx.WebSocket is not { State: WebSocketState.Open }) return;

        await _ctx.WsSendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_ctx.WebSocket is not { State: WebSocketState.Open }) return;

            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
            await _ctx.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (WebSocketException ex)
        {
            Log.Warning(ex, "[RealtimeAi] Failed to send to client, SessionId: {SessionId}, WebSocketState: {WebSocketState}",
                _ctx.SessionId, _ctx.WebSocket?.State);
        }
        finally
        {
            _ctx.WsSendLock.Release();
        }
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
