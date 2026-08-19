using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The recording path runs on every inbound frame — fifty a second per call — and allocated twice
/// per frame: once to decode the whole frame to PCM, once more to resample it. The decode exists
/// only to be read back sample by sample immediately afterwards.
///
/// <para>Fusing the two removes the intermediate array. The output has to be bit-for-bit what it was,
/// and the golden suite does not cover <c>ConvertForRecording</c>, so equivalence is proven here
/// directly: against the composition of the two public primitives the old implementation used, over
/// random and edge-case inputs across both telephony codecs.</para>
/// </summary>
public class AudioCodecConverterRecordingEquivalenceTests
{
    private const int RecordingRate = 24000;

    /// <summary>Exactly what ConvertForRecording used to do: decode everything, then resample.</summary>
    private static byte[] PreviousImplementation(byte[] audio, RealtimeAiAudioCodec codec, int sourceRate)
    {
        var pcm = codec == RealtimeAiAudioCodec.PCM16
            ? audio
            : AudioCodecConverter.Convert(audio, codec, RealtimeAiAudioCodec.PCM16);

        return AudioCodecConverter.Resample(pcm, sourceRate, RecordingRate);
    }

    private static byte[] RandomBytes(int length, int seed)
    {
        var random = new Random(seed);
        var bytes = new byte[length];
        random.NextBytes(bytes);

        return bytes;
    }

    [Theory]
    [InlineData(RealtimeAiAudioCodec.MULAW, 8000)]
    [InlineData(RealtimeAiAudioCodec.ALAW, 8000)]
    [InlineData(RealtimeAiAudioCodec.PCM16, 24000)]
    [InlineData(RealtimeAiAudioCodec.PCM16, 16000)]
    public void FusedConversion_ShouldMatchThePreviousImplementationByteForByte(RealtimeAiAudioCodec codec, int sourceRate)
    {
        // 160 bytes of mulaw is one 20ms telephony frame — the real shape of this path.
        foreach (var length in new[] { 0, 1, 2, 3, 160, 161, 320, 1600 })
        {
            // PCM16 samples are two bytes; an odd length would not occur and the two paths round it
            // differently, which is not a behaviour worth pinning either way.
            if (codec == RealtimeAiAudioCodec.PCM16 && length % 2 != 0) continue;

            var input = RandomBytes(length, seed: length + (int)codec);

            AudioCodecConverter.ConvertForRecording(input, codec, sourceRate)
                .ShouldBe(PreviousImplementation(input, codec, sourceRate), $"length {length}, codec {codec}");
        }
    }

    [Theory]
    [InlineData(RealtimeAiAudioCodec.MULAW)]
    [InlineData(RealtimeAiAudioCodec.ALAW)]
    public void FusedConversion_ShouldMatchAcrossTheFullByteRange(RealtimeAiAudioCodec codec)
    {
        // Every possible encoded value, so no companding table entry can differ between the paths.
        var everyByte = new byte[256];
        for (var i = 0; i < 256; i++) everyByte[i] = (byte)i;

        AudioCodecConverter.ConvertForRecording(everyByte, codec, 8000)
            .ShouldBe(PreviousImplementation(everyByte, codec, 8000));
    }

    [Fact]
    public void SourceAlreadyAtRecordingRate_ShouldStillMatch()
    {
        // The no-resample branch: the previous implementation returned the decoded array as-is.
        var input = RandomBytes(480, seed: 7);

        AudioCodecConverter.ConvertForRecording(input, RealtimeAiAudioCodec.PCM16, RecordingRate)
            .ShouldBe(PreviousImplementation(input, RealtimeAiAudioCodec.PCM16, RecordingRate));
    }

    [Fact]
    public void OutputLength_ShouldStayAtTwoBytesPerResampledSample()
    {
        // 160 mulaw samples at 8kHz become 480 at 24kHz, two bytes each.
        AudioCodecConverter.ConvertForRecording(new byte[160], RealtimeAiAudioCodec.MULAW, 8000)
            .Length.ShouldBe(480 * 2);
    }
}
