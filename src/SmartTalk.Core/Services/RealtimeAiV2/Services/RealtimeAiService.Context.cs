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
    }

    private void BuildRecordingIfRequired()
    {
        if (_ctx.Options.EnableRecording && _ctx.AudioBuffer == null) _ctx.AudioBuffer = new MemoryStream();
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
    }
}
