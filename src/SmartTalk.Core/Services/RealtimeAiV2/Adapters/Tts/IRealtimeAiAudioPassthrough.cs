namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;

/// <summary>
/// Capability sibling of <see cref="IRealtimeAiTtsProvider"/> for the AUDIO output mode: the provider
/// passes the inference provider's native audio straight through (no synthesis). The engine routes
/// provider audio here only when the resolved output mode is audio.
/// </summary>
public interface IRealtimeAiAudioPassthrough
{
    /// <summary>Forward a base64 audio chunk emitted by the inference provider.</summary>
    Task HandleProviderAudioAsync(string base64Audio, CancellationToken cancellationToken);

    /// <summary>The inference provider finished emitting audio for the turn; raise the terminal signal.</summary>
    Task HandleProviderAudioDoneAsync(CancellationToken cancellationToken);
}
