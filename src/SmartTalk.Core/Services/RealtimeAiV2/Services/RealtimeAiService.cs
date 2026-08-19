using System.Security.Cryptography;
using System.Text;
using Serilog;
using Serilog.Context;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Logging;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Enums.RealtimeAi;

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

        LogSessionInitialized();

        try
        {
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
        finally
        {
            // A session that dies at connect used to leave no trace at all: the throw skipped
            // OrchestrateSessionAsync, and with it the only call to CleanupSessionAsync — so no
            // OnSessionEnded, no transcript, no recording, and nothing for an operator to find.
            // The claim inside makes this a no-op when the orchestration loop already ran it.
            await CleanupSessionAsync(clientIsClose: false).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Describes the session without reproducing its content. This deliberately replaced a
    /// <c>{@Context}</c> destructure of the whole session context, which reached
    /// <c>Options.ModelConfig.Prompt</c> — for a phone call, the resolved restaurant prompt carrying
    /// the caller's number, their CRM record and the menu — and wrote it to Seq and stdout on every
    /// inbound call, before the caller heard the greeting.
    ///
    /// <para>The prompt is represented by its length and a short hash, which is enough to confirm
    /// which revision was live without the sink retaining any of it. Guarded by
    /// RealtimeAiServiceLogPrivacyTests.</para>
    /// </summary>
    private void LogSessionInitialized()
    {
        var model = _ctx.Options.ModelConfig;

        Log.Information(
            "[RealtimeAi] Session initialized, SessionId: {SessionId}, Provider: {Provider}, Client: {Client}, " +
            "Region: {Region}, TtsProvider: {TtsProvider}, Recording: {Recording}, " +
            "PromptChars: {PromptChars}, PromptSha256: {PromptSha256}, ToolCount: {ToolCount}",
            _ctx.SessionId, model.Provider, _ctx.Options.ClientConfig.Client,
            _ctx.Options.Region, _ctx.Options.TtsConfig?.ProviderType ?? RealtimeAiTtsProviderType.BuiltIn, _ctx.Options.EnableRecording,
            model.Prompt?.Length ?? 0, ShortHash(model.Prompt), model.Tools?.Count ?? 0);
    }

    /// <summary>First 8 hex chars of the SHA-256 — identifies a revision, reveals nothing about it.</summary>
    private static string ShortHash(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..8].ToLowerInvariant();
    }

    private string GetWebSocketStateSafe()
    {
        try { return _ctx.WebSocket?.State.ToString() ?? "null"; }
        catch { return "unknown"; }
    }
}
