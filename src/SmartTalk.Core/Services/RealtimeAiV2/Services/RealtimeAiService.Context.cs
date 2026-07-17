using SmartTalk.Core.Services.RealtimeAiV2.Recording;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    private void BuildSessionContext(RealtimeSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.ClientConfig);
        ArgumentNullException.ThrowIfNull(options.ModelConfig);
        ArgumentNullException.ThrowIfNull(options.ModelConfig.ServiceUrl);
        ArgumentNullException.ThrowIfNull(options.ConnectionProfile);

        _ctx = new RealtimeAiSessionContext
        {
            Options = options,
            WebSocket = options.WebSocket,
            SessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        };

        BuildConnectSwitcher();
        BuildRecordingIfRequired();
        BuildSessionActions();
        ApplyMaxSessionDurationIfRequired();
    }

    private void ApplyMaxSessionDurationIfRequired()
    {
        if (_ctx.Options.MaxSessionDuration is not { } maxSessionDuration || maxSessionDuration <= TimeSpan.Zero)
            return;

        _ctx.SessionCts.CancelAfter(maxSessionDuration);
    }

    private void BuildRecordingIfRequired()
    {
        // RealtimeAiRecordingSettings.Create() picks UnboundedMemoryBuffer (default) or
        // RollingWindowBuffer based on the BufferMode env var. Default preserves the
        // pre-Phase-3 unbounded behaviour exactly.
        if (_ctx.Options.EnableRecording && _ctx.AudioBuffer == null) _ctx.AudioBuffer = RealtimeAiRecordingSettings.Create();
    }

    private void BuildSessionActions()
    {
        _ctx.SessionActions = new RealtimeAiSessionActions
        {
            SendAudioToClientAsync = SendAudioToClientAsync,
            SendTextToProviderAsync = SendTextToProviderAsync,
            SuspendClientAudioToProvider = () => _ctx.IsClientAudioToProviderSuspended = true,
            ResumeClientAudioToProvider = () => _ctx.IsClientAudioToProviderSuspended = false,
            GetRecordedAudioSnapshotAsync = GetRecordedAudioSnapshotAsync
        };
    }

    private void BuildConnectSwitcher()
    {
        _ctx.WssClient = _realtimeAiSwitcher.WssClient(_ctx.Options.ModelConfig.Provider);
        _ctx.ClientAdapter = _realtimeAiSwitcher.ClientAdapter(_ctx.Options.ClientConfig.Client);
        _ctx.ProviderAdapter = _realtimeAiSwitcher.ProviderAdapter(_ctx.Options.ModelConfig.Provider);
        _ctx.TtsProvider = _realtimeAiSwitcher.TtsProvider(_ctx.Options.TtsConfig?.ProviderType ?? RealtimeAiTtsProviderType.BuiltIn);
    }
}
