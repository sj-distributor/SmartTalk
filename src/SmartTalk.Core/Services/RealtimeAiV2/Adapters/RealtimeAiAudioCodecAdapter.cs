using NAudio.Codecs;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters;

public interface IRealtimeAiAudioCodecAdapter : IScopedDependency
{
    bool IsConversionSupported(RealtimeAiAudioCodec inputCodec, int inputSampleRate, RealtimeAiAudioCodec outputCodec, int outputSampleRate);

    Task<byte[]> ConvertAsync(byte[] inputAudioBytes, RealtimeAiAudioCodec inputCodec, int inputSampleRate, RealtimeAiAudioCodec outputCodec, int outputSampleRate, CancellationToken cancellationToken);
}

public class RealtimeAiAudioCodecAdapter : IRealtimeAiAudioCodecAdapter
{
    public async Task<byte[]> ConvertAsync(byte[] inputAudioBytes, RealtimeAiAudioCodec inputCodec,
        int inputSampleRate, RealtimeAiAudioCodec outputCodec, int outputSampleRate, CancellationToken cancellationToken)
    {
        if (inputCodec == outputCodec && inputSampleRate == outputSampleRate) return inputAudioBytes;
        
        if (!IsConversionSupported(inputCodec, inputSampleRate, outputCodec, outputSampleRate))
             throw new NotSupportedException($"不支持从 {inputCodec} @ {inputSampleRate}Hz 到 {outputCodec} @ {outputSampleRate}Hz 的音频转换。");

        return inputCodec switch
        {
            RealtimeAiAudioCodec.ALAW when outputCodec == RealtimeAiAudioCodec.PCM16 =>
                await ConvertALawWavBytesToPcmBytes(inputAudioBytes, outputSampleRate),
            RealtimeAiAudioCodec.MULAW when outputCodec == RealtimeAiAudioCodec.PCM16 =>
                await ConvertULawWavBytesToPcmBytes(inputAudioBytes, outputSampleRate),
            RealtimeAiAudioCodec.PCM16 when outputCodec == RealtimeAiAudioCodec.MULAW =>
                await ConvertPcmBytesToULawBytes(inputAudioBytes),
            RealtimeAiAudioCodec.PCM16 when outputCodec == RealtimeAiAudioCodec.ALAW =>
                await ConvertPcmBytesToALawBytes(inputAudioBytes),
            _ => throw new NotSupportedException(
                $"不支持从 {inputCodec} @ {inputSampleRate}Hz 到 {outputCodec} @ {outputSampleRate}Hz 的音频转换。")
        };
    }
    
    public bool IsConversionSupported(RealtimeAiAudioCodec inputCodec, int inputSampleRate, RealtimeAiAudioCodec outputCodec, int outputSampleRate)
    {
        switch (inputCodec)
        {
            case RealtimeAiAudioCodec.ALAW when outputCodec == RealtimeAiAudioCodec.MULAW:
            case RealtimeAiAudioCodec.MULAW when outputCodec == RealtimeAiAudioCodec.ALAW:
            case RealtimeAiAudioCodec.ALAW when outputCodec == RealtimeAiAudioCodec.PCM16 && (inputSampleRate != 8000 || outputSampleRate != 8000):
            case RealtimeAiAudioCodec.MULAW when outputCodec == RealtimeAiAudioCodec.PCM16 && (inputSampleRate != 8000 || outputSampleRate != 8000):
            case RealtimeAiAudioCodec.PCM16 when outputCodec == RealtimeAiAudioCodec.ALAW && outputSampleRate != 8000:
            case RealtimeAiAudioCodec.PCM16 when outputCodec == RealtimeAiAudioCodec.MULAW && outputSampleRate != 8000:
                return false;
            default:
                return true;
        }
    }
    
     public static async Task<byte[]> ConvertPcmBytesToULawBytes(byte[] pcmWavBytes) 
     {
        if (pcmWavBytes == null || pcmWavBytes.Length == 0) throw new ArgumentNullException(nameof(pcmWavBytes));

        using var pcmStream = new MemoryStream(pcmWavBytes);
        await using var reader = new WaveFileReader(pcmStream);
        var sourceSamples = reader.ToSampleProvider();

        switch (sourceSamples.WaveFormat.Channels)
        {
            case 2:
                sourceSamples = new StereoToMonoSampleProvider(sourceSamples);
                break;
            case > 2:
                sourceSamples = sourceSamples.ToStereo();
                break;
        }
        
        var outputChannels = 1;

        var uLawFormat = WaveFormat.CreateMuLawFormat(8000, outputChannels);
        var encodedBytes = new List<byte>();
        var readBuffer = new float[sourceSamples.WaveFormat.SampleRate * sourceSamples.WaveFormat.Channels]; // 1 sec buffer
        int samplesRead;

        while ((samplesRead = sourceSamples.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            for (var i = 0; i < samplesRead; i++)
            {
                var pcmSample = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, readBuffer[i] * 32768.0f));
                encodedBytes.Add(MuLawEncoder.LinearToMuLawSample(pcmSample));
            }
        }

        using var outputStream = new MemoryStream();
        await using var writer = new WaveFileWriter(outputStream, uLawFormat);
        writer.Write(encodedBytes.ToArray(), 0, encodedBytes.Count);
        writer.Flush();
        return outputStream.ToArray();
     }

    public static async Task<byte[]> ConvertPcmBytesToALawBytes(byte[] pcmWavBytes)
    {
        if (pcmWavBytes == null || pcmWavBytes.Length == 0) throw new ArgumentNullException(nameof(pcmWavBytes));

        using var pcmStream = new MemoryStream(pcmWavBytes);
        await using var reader = new WaveFileReader(pcmStream);
        var sourceSamples = reader.ToSampleProvider();

        switch (sourceSamples.WaveFormat.Channels)
        {
            case 2:
                sourceSamples = new StereoToMonoSampleProvider(sourceSamples);
                break;
            case > 2:
                sourceSamples = sourceSamples.ToStereo();
                break;
        }
        
        const int outputChannels = 1;

        var aLawFormat = WaveFormat.CreateALawFormat(8000, outputChannels);
        var encodedBytes = new List<byte>();
        var readBuffer = new float[sourceSamples.WaveFormat.SampleRate * sourceSamples.WaveFormat.Channels];
        int samplesRead;

        while ((samplesRead = sourceSamples.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            for (var i = 0; i < samplesRead; i++)
            {
                var pcmSample = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, readBuffer[i] * 32768.0f));
                encodedBytes.Add(ALawEncoder.LinearToALawSample(pcmSample));
            }
        }

        using var outputStream = new MemoryStream();
        await using var writer = new WaveFileWriter(outputStream, aLawFormat);
        writer.Write(encodedBytes.ToArray(), 0, encodedBytes.Count);
        writer.Flush();
        return outputStream.ToArray();
    }

    public static async Task<byte[]> ConvertULawWavBytesToPcmBytes(byte[] uLawWavBytes, int targetPcmSampleRate, int targetPcmBitDepth = 16, int targetPcmChannels = 1)
    {
        if (uLawWavBytes == null || uLawWavBytes.Length == 0) throw new ArgumentNullException(nameof(uLawWavBytes));

        using var memoryStream = new MemoryStream();

        await using var writer = new WaveFileWriter(memoryStream, new WaveFormat(targetPcmSampleRate, targetPcmBitDepth, channels: targetPcmChannels));
        
        foreach (var t in uLawWavBytes)
        {
            var pcmSample = MuLawDecoder.MuLawToLinearSample(t);
            writer.WriteSample(pcmSample / 32768f);
        }
        
        return memoryStream.ToArray();
    }

    public static async Task<byte[]> ConvertALawWavBytesToPcmBytes(byte[] aLawWavBytes, int targetPcmSampleRate, int targetPcmBitDepth = 16, int targetPcmChannels = 1)
    {
        if (aLawWavBytes == null || aLawWavBytes.Length == 0) throw new ArgumentNullException(nameof(aLawWavBytes));

        using var memoryStream = new MemoryStream();

        await using var writer = new WaveFileWriter(memoryStream, new WaveFormat(targetPcmSampleRate, targetPcmBitDepth, channels: targetPcmChannels));
        
        foreach (var t in aLawWavBytes)
        {
            var pcmSample = ALawDecoder.ALawToLinearSample(t);
            writer.WriteSample(pcmSample / 32768f);
        }
        
        return memoryStream.ToArray();
    }
}