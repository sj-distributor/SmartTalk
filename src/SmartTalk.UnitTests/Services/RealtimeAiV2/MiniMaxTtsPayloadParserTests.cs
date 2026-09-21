using System.Text;
using System.Text.Json;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.MiniMax;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// The MiniMax payload parser decides what the caller actually hears: it pulls PCM out of the WAV the
/// vendor sends and reports the rate the engine resamples from. Getting either wrong produces audio
/// that is noise, the wrong speed, or silence — and none of it was covered.
///
/// <para>It is pure and static, so it can be pinned exactly, without touching the 759-line provider
/// around it.</para>
/// </summary>
public class MiniMaxTtsPayloadParserTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>A minimal RIFF/WAVE container, optionally with a chunk before fmt to force the walk.</summary>
    private static byte[] Wav(byte[] pcm, int sampleRate = 24000, short channels = 1, short bits = 16,
        short audioFormat = 1, byte[] extraChunk = null)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(0);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));

        if (extraChunk != null) w.Write(extraChunk);

        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write(audioFormat);
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(sampleRate * channels * bits / 8);
        w.Write((short)(channels * bits / 8));
        w.Write(bits);

        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(pcm.Length);
        w.Write(pcm);

        return ms.ToArray();
    }

    [Fact]
    public void SampleRate_NestedInExtraInfo_ShouldWin()
    {
        // MiniMax reports it under extra_info; the root-level read is the fallback.
        MiniMaxRealtimeAiTtsPayloadParser
            .TryGetAudioSampleRate(Json("""{"extra_info":{"audio_sample_rate":24000},"audio_sample_rate":8000}"""), out var rate)
            .ShouldBeTrue();

        rate.ShouldBe(24000);
    }

    [Theory]
    [InlineData("""{"audio_sample_rate":16000}""", 16000)]
    [InlineData("""{"audio_sample_rate":"16000"}""", 16000)]
    [InlineData("""{"extra_info":{"audio_sample_rate":"8000"}}""", 8000)]
    public void SampleRate_ShouldAcceptNumberOrStringForm(string json, int expected)
    {
        // The vendor has sent both shapes; a rate read as zero makes the engine resample from nothing.
        MiniMaxRealtimeAiTtsPayloadParser.TryGetAudioSampleRate(Json(json), out var rate).ShouldBeTrue();

        rate.ShouldBe(expected);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"extra_info":{}}""")]
    [InlineData("""{"audio_sample_rate":"not-a-number"}""")]
    [InlineData("""{"extra_info":"not-an-object"}""")]
    public void SampleRate_WhenAbsentOrUnparseable_ShouldReportFailureRatherThanZero(string json)
    {
        MiniMaxRealtimeAiTtsPayloadParser.TryGetAudioSampleRate(Json(json), out _).ShouldBeFalse();
    }

    [Fact]
    public void Wav_ShouldYieldItsPcmAndRate()
    {
        var pcm = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        MiniMaxRealtimeAiTtsPayloadParser.TryExtractWavPcm16(Wav(pcm), out var rate, out var extracted).ShouldBeTrue();

        rate.ShouldBe(24000);
        extracted.ShouldBe(pcm);
    }

    [Fact]
    public void Wav_WithAnOddSizedChunkBeforeFmt_ShouldStillParse()
    {
        // RIFF pads odd chunks to even boundaries; missing that walks into the middle of a header.
        var oddChunk = new List<byte>();
        oddChunk.AddRange(Encoding.ASCII.GetBytes("LIST"));
        oddChunk.AddRange(BitConverter.GetBytes(3));
        oddChunk.AddRange(new byte[] { 9, 9, 9, 0 });

        var pcm = new byte[] { 10, 20, 30, 40 };

        MiniMaxRealtimeAiTtsPayloadParser
            .TryExtractWavPcm16(Wav(pcm, extraChunk: oddChunk.ToArray()), out _, out var extracted)
            .ShouldBeTrue();

        extracted.ShouldBe(pcm);
    }

    [Theory]
    [InlineData(2, (short)16, (short)1)]    // stereo — the engine's pipeline is mono
    [InlineData(1, (short)8, (short)1)]     // 8-bit — not PCM16
    [InlineData(1, (short)16, (short)3)]    // IEEE float, not integer PCM
    public void Wav_ThatIsNotMonoPcm16_ShouldBeRejected(int channels, short bits, short audioFormat)
    {
        // Accepting these would hand the resampler bytes it interprets as something else entirely.
        MiniMaxRealtimeAiTtsPayloadParser
            .TryExtractWavPcm16(Wav(new byte[8], channels: (short)channels, bits: bits, audioFormat: audioFormat), out _, out _)
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3 })]
    public void Wav_ThatIsTooShortOrNotRiff_ShouldBeRejected(byte[] bytes)
    {
        MiniMaxRealtimeAiTtsPayloadParser.TryExtractWavPcm16(bytes, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void AudioPayload_Base64_ShouldDecode()
    {
        var expected = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var json = """{"data":{"audio":"PLACEHOLDER"}}""".Replace("PLACEHOLDER", Convert.ToBase64String(expected));

        MiniMaxRealtimeAiTtsPayloadParser.TryGetAudioPayload(Json(json), out var bytes).ShouldBeTrue();

        bytes.ShouldBe(expected);
    }

    [Fact]
    public void AudioPayload_Hex_ShouldDecode()
    {
        MiniMaxRealtimeAiTtsPayloadParser
            .TryGetAudioPayload(Json("""{"data":{"audio":"deadbeef"}}"""), out var bytes).ShouldBeTrue();

        bytes.ShouldBe(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
    }

    [Fact]
    public void AudioPayload_ThatIsValidAsBothEncodings_IsReadAsHex()
    {
        // A real ambiguity, pinned rather than assumed: "abcdef12" is eight characters, so it is
        // simultaneously legal hex and a legal base64 length, and the parser tries hex first. Both
        // encodings are in use, so the tie-break is load-bearing — resolving it the other way turns
        // a chunk of speech into noise.
        MiniMaxRealtimeAiTtsPayloadParser
            .TryGetAudioPayload(Json("""{"data":{"audio":"abcdef12"}}"""), out var bytes).ShouldBeTrue();

        bytes.ShouldBe(new byte[] { 0xAB, 0xCD, 0xEF, 0x12 });
        bytes.ShouldNotBe(Convert.FromBase64String("abcdef12"), "the two readings genuinely differ");
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"data":"not-an-object"}""")]
    [InlineData("""{"data":{}}""")]
    [InlineData("""{"data":{"audio":""}}""")]
    [InlineData("""{"data":{"audio":"   "}}""")]
    [InlineData("""{"data":{"audio":"!!!not-encodable!!!"}}""")]
    public void AudioPayload_WhenAbsentOrUndecodable_ShouldReportFailure(string json)
    {
        MiniMaxRealtimeAiTtsPayloadParser.TryGetAudioPayload(Json(json), out var bytes).ShouldBeFalse();

        bytes.ShouldBeEmpty();
    }
}
