using Shouldly;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Pins the literal Description string of every <see cref="RealtimeAiAudioCodec"/>
/// value to the exact wire format expected by the OpenAI Realtime API
/// (see https://platform.openai.com/docs/api-reference/realtime-client-events).
///
/// These literals are sent verbatim in the <c>session.update</c> payload's
/// <c>input_audio_format</c> / <c>output_audio_format</c> fields. Any deviation
/// (case, hyphen, prefix) makes OpenAI reject the session with
/// <c>invalid_request_error</c> and the call dies before the AI ever speaks.
///
/// <para>
/// A regression here is unrecoverable in production — pin every literal so that
/// renaming the enum or "fixing" a Description silently breaks live calls.
/// </para>
/// </summary>
public class RealtimeAiAudioCodecDescriptionPinningTests
{
    [Theory]
    [InlineData(RealtimeAiAudioCodec.MULAW, "g711_ulaw")]
    [InlineData(RealtimeAiAudioCodec.ALAW, "g711_alaw")]
    [InlineData(RealtimeAiAudioCodec.PCM16, "pcm16")]
    public void GetDescription_ReturnsOpenAiWireFormatLiteral(RealtimeAiAudioCodec codec, string expected)
    {
        codec.GetDescription().ShouldBe(expected);
    }
}
