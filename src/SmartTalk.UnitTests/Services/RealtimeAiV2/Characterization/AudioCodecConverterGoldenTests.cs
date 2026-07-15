using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2.Characterization;

/// <summary>
/// CHARACTERIZATION test — pins the EXACT output BYTES of the codec and resample DSP, captured from
/// the real <see cref="AudioCodecConverter"/>. The existing AudioCodecConverterTests assert only
/// output LENGTH (and a lossless re-encode roundtrip), so a swapped NAudio table, flipped endianness,
/// or changed rounding ships a different audio stream green. These golden vectors fail RED on any
/// byte-level drift. Guards S6 (TTS split touches the output-encode legs) and S9 (OrderedAudioBus).
///
/// Golden hex strings were captured by running the real converter once and frozen.
/// </summary>
public class AudioCodecConverterGoldenTests
{
    private static byte[] Pcm16(params short[] samples)
    {
        var b = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            b[i * 2] = (byte)(samples[i] & 0xFF);
            b[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
        }
        return b;
    }

    private static string Hex(byte[] b) => System.Convert.ToHexString(b);

    private static short FirstSample(byte[] pcm16) => (short)(pcm16[0] | (pcm16[1] << 8));

    [Fact]
    public void MulawToPcm16_GoldenBytes()
    {
        var input = new byte[] { 0x00, 0x7F, 0x80, 0xFF, 0xD5 };

        Hex(AudioCodecConverter.Convert(input, RealtimeAiAudioCodec.MULAW, RealtimeAiAudioCodec.PCM16))
            .ShouldBe("8482FFFF7C7D0000CC02");
    }

    [Fact]
    public void Pcm16ToMulaw_GoldenBytes()
    {
        var input = Pcm16(0, 1000, -1000, 32767, -32768);

        Hex(AudioCodecConverter.Convert(input, RealtimeAiAudioCodec.PCM16, RealtimeAiAudioCodec.MULAW))
            .ShouldBe("FFCE4E807F");
    }

    [Fact]
    public void Pcm16ToAlaw_GoldenBytes_SilenceEncodesToD5()
    {
        var input = Pcm16(0, 1000, -1000, 32767, -32768);

        var alaw = AudioCodecConverter.Convert(input, RealtimeAiAudioCodec.PCM16, RealtimeAiAudioCodec.ALAW);

        Hex(alaw).ShouldBe("D5FA7AAA55");
        alaw[0].ShouldBe((byte)0xD5);   // PCM16 silence (0) → A-law 0xD5
    }

    [Fact]
    public void AlawToPcm16_GoldenBytes_SilenceByteDecodesToMinus8()
    {
        var input = new byte[] { 0x55, 0xD5, 0x00, 0xFF, 0x2A };

        var pcm = AudioCodecConverter.Convert(input, RealtimeAiAudioCodec.ALAW, RealtimeAiAudioCodec.PCM16);

        Hex(pcm).ShouldBe("F8FF080080EA50030082");
        FirstSample(pcm).ShouldBe((short)-8);   // A-law 0x55 → PCM16 -8 (non-zero-preserving silence)
    }

    [Fact]
    public void Resample_8kTo24k_GoldenBytes_NegativeTruncationTowardZero()
    {
        // 4 samples → 12; the 1/3 and 2/3 interpolations are non-integer and negative for the
        // 100 → -100 transition (e.g. -33.3 truncates toward zero to -33, not -34).
        var input = Pcm16(100, -100, 300, -8);

        Hex(AudioCodecConverter.Resample(input, 8000, 24000))
            .ShouldBe("64002100DFFF9CFF2100A6002C01C5005E00F8FFF8FFF8FF");
    }

    [Fact]
    public void Resample_24kTo8k_GoldenBytes_Decimation()
    {
        // 9 samples → 3; ratio 3.0 selects indices 0, 3, 6 (values 0, 300, 600).
        var input = Pcm16(0, 100, 200, 300, 400, 500, 600, 700, 800);

        Hex(AudioCodecConverter.Resample(input, 24000, 8000)).ShouldBe("00002C015802");
    }
}
