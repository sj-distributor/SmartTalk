using NAudio.Codecs;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters;

public static class AudioCodecConverter
{
    private const int RecordingSampleRate = 24000;

    public static byte[] Convert(byte[] audio, RealtimeAiAudioCodec from, RealtimeAiAudioCodec to)
    {
        if (from == to) return audio;

        return (from, to) switch
        {
            (RealtimeAiAudioCodec.MULAW, RealtimeAiAudioCodec.PCM16) => MulawToPcm16(audio),
            (RealtimeAiAudioCodec.PCM16, RealtimeAiAudioCodec.MULAW) => Pcm16ToMulaw(audio),
            (RealtimeAiAudioCodec.ALAW, RealtimeAiAudioCodec.PCM16) => AlawToPcm16(audio),
            (RealtimeAiAudioCodec.PCM16, RealtimeAiAudioCodec.ALAW) => Pcm16ToAlaw(audio),
            _ => throw new NotSupportedException($"Audio conversion from {from} to {to} is not supported.")
        };
    }

    /// <summary>
    /// Converts audio to PCM16 at 24kHz for recording buffer.
    /// Handles both codec conversion and sample rate resampling.
    /// </summary>
    public static byte[] ConvertForRecording(byte[] audio, RealtimeAiAudioCodec sourceCodec)
    {
        return ConvertForRecording(audio, sourceCodec, GetSampleRate(sourceCodec));
    }

    public static byte[] ConvertForRecording(byte[] audio, RealtimeAiAudioCodec sourceCodec, int sourceSampleRate)
    {
        if (sourceCodec == RealtimeAiAudioCodec.PCM16) return Resample(audio, sourceSampleRate, RecordingSampleRate);

        if (sourceSampleRate == RecordingSampleRate) return Convert(audio, sourceCodec, RealtimeAiAudioCodec.PCM16);

        return ResampleDecoding(audio, sourceCodec, sourceSampleRate, RecordingSampleRate);
    }

    /// <summary>
    /// Decodes and resamples in one pass. This runs on every inbound frame — fifty a second per call
    /// — and the separate decode existed only to be read back sample by sample immediately
    /// afterwards, so it was one whole-frame allocation per frame with no other purpose.
    ///
    /// <para>Output is bit-for-bit what decode-then-resample produced: the interpolation reads the
    /// same sample values in the same order, it just decodes them on demand rather than up front.
    /// Proven against the previous composition in AudioCodecConverterRecordingEquivalenceTests.</para>
    /// </summary>
    private static byte[] ResampleDecoding(byte[] encoded, RealtimeAiAudioCodec codec, int fromRate, int toRate)
    {
        // One byte per sample: both telephony codecs this handles are 8-bit companded.
        var sampleCount = encoded.Length;
        var newSampleCount = (int)((long)sampleCount * toRate / fromRate);
        var result = new byte[newSampleCount * 2];
        var ratio = (double)fromRate / toRate;

        for (var i = 0; i < newSampleCount; i++)
        {
            var srcPos = i * ratio;
            var srcIndex = (int)srcPos;
            var frac = srcPos - srcIndex;

            short sample;

            if (srcIndex >= sampleCount - 1)
            {
                sample = DecodeSample(encoded, sampleCount - 1, codec);
            }
            else
            {
                var s1 = DecodeSample(encoded, srcIndex, codec);
                var s2 = DecodeSample(encoded, srcIndex + 1, codec);
                sample = (short)(s1 + (s2 - s1) * frac);
            }

            result[i * 2] = (byte)(sample & 0xFF);
            result[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return result;
    }

    private static short DecodeSample(byte[] encoded, int index, RealtimeAiAudioCodec codec) => codec switch
    {
        RealtimeAiAudioCodec.MULAW => MuLawDecoder.MuLawToLinearSample(encoded[index]),
        RealtimeAiAudioCodec.ALAW => ALawDecoder.ALawToLinearSample(encoded[index]),
        _ => throw new NotSupportedException($"Cannot decode {codec} one sample at a time.")
    };

    public static int GetSampleRate(RealtimeAiAudioCodec codec) => codec switch
    {
        RealtimeAiAudioCodec.MULAW => 8000,
        RealtimeAiAudioCodec.ALAW => 8000,
        _ => 24000
    };

    public static byte[] Resample(byte[] pcm16, int fromRate, int toRate)
    {
        if (fromRate == toRate) return pcm16;

        var sampleCount = pcm16.Length / 2;
        var newSampleCount = (int)((long)sampleCount * toRate / fromRate);
        var result = new byte[newSampleCount * 2];
        var ratio = (double)fromRate / toRate;

        for (var i = 0; i < newSampleCount; i++)
        {
            var srcPos = i * ratio;
            var srcIndex = (int)srcPos;
            var frac = srcPos - srcIndex;

            short sample;

            if (srcIndex >= sampleCount - 1)
            {
                sample = ReadSample(pcm16, sampleCount - 1);
            }
            else
            {
                var s1 = ReadSample(pcm16, srcIndex);
                var s2 = ReadSample(pcm16, srcIndex + 1);
                sample = (short)(s1 + (s2 - s1) * frac);
            }

            result[i * 2] = (byte)(sample & 0xFF);
            result[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return result;
    }

    private static short ReadSample(byte[] pcm16, int sampleIndex) =>
        (short)(pcm16[sampleIndex * 2] | (pcm16[sampleIndex * 2 + 1] << 8));

    private static byte[] MulawToPcm16(byte[] mulaw)
    {
        var pcm = new byte[mulaw.Length * 2];
        for (var i = 0; i < mulaw.Length; i++)
        {
            var sample = MuLawDecoder.MuLawToLinearSample(mulaw[i]);
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return pcm;
    }

    private static byte[] Pcm16ToMulaw(byte[] pcm)
    {
        var mulaw = new byte[pcm.Length / 2];
        for (var i = 0; i < mulaw.Length; i++)
        {
            var sample = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
            mulaw[i] = MuLawEncoder.LinearToMuLawSample(sample);
        }
        return mulaw;
    }

    private static byte[] AlawToPcm16(byte[] alaw)
    {
        var pcm = new byte[alaw.Length * 2];
        for (var i = 0; i < alaw.Length; i++)
        {
            var sample = ALawDecoder.ALawToLinearSample(alaw[i]);
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return pcm;
    }

    private static byte[] Pcm16ToAlaw(byte[] pcm)
    {
        var alaw = new byte[pcm.Length / 2];
        for (var i = 0; i < alaw.Length; i++)
        {
            var sample = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
            alaw[i] = ALawEncoder.LinearToALawSample(sample);
        }
        return alaw;
    }
}
