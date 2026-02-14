using NAudio.Codecs;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters;

public static class AudioCodecConverter
{
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
