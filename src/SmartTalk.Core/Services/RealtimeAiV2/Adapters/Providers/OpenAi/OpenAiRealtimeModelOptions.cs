namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;

/// <summary>
/// OpenAI-specific realtime session options, owned by the OpenAI adapter and carried opaquely on
/// <c>RealtimeAiModelConfig.VendorOptions</c> so the shared model config stays vendor-agnostic. The
/// consumer populates this for OpenAI assistants; the OpenAI adapter reads it. Each field's exact wire
/// mapping and null-handling is documented on <see cref="OpenAiRealtimeAiProviderAdapter"/>.
/// </summary>
public sealed class OpenAiRealtimeModelOptions
{
    /// <summary>Input audio noise-reduction config, emitted under session.audio.input.noise_reduction.</summary>
    public object InputAudioNoiseReduction { get; init; }

    /// <summary>Transcription language hint (ISO-639-1 or "yue"); null omits the language property.</summary>
    public string TranscriptionLanguage { get; init; }

    /// <summary>Transcription model override; null/empty uses the adapter default.</summary>
    public string TranscriptionModel { get; init; }

    /// <summary>Cap on response output tokens; null omits session.max_response_output_tokens.</summary>
    public int? MaxResponseOutputTokens { get; init; }

    /// <summary>Output audio playback speed; null omits session.audio.output.speed.</summary>
    public decimal? OutputAudioSpeed { get; init; }

    /// <summary>Opt-in to OpenAI session tracing; true emits session.tracing = "auto".</summary>
    public bool? EnableRealtimeTracing { get; init; }
}
