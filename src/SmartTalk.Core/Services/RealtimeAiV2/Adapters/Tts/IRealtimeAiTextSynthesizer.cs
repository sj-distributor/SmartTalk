namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;

/// <summary>
/// Capability sibling of <see cref="IRealtimeAiTtsProvider"/> for the TEXT output mode: the provider
/// synthesizes audio from the inference provider's streamed text. The engine routes provider text here
/// only when the resolved output mode is text.
/// </summary>
public interface IRealtimeAiTextSynthesizer
{
    /// <summary>Accept a streamed text fragment from the inference provider.</summary>
    Task HandleProviderTextDeltaAsync(string textDelta, CancellationToken cancellationToken);

    /// <summary>The inference provider finished emitting text for the turn; finalize synthesis and raise the terminal signal.</summary>
    Task HandleProviderTextDoneAsync(CancellationToken cancellationToken);
}
