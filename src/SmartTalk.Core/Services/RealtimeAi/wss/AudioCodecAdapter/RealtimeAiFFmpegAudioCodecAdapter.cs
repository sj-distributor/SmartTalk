using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Messages.Enums.System;

namespace SmartTalk.Core.Services.RealtimeAi.wss.AudioCodecAdapter;

public class RealtimeAiFFmpegAudioCodecAdapter : IRealtimeAiAudioCodecAdapter
{
    private readonly IFfmpegService _ffmpegService;

    public RealtimeAiFFmpegAudioCodecAdapter(IFfmpegService ffmpegService)
    {
        _ffmpegService = ffmpegService;
    }

    public bool IsConversionSupported(AudioCodec inputCodec, int inputSampleRate, AudioCodec outputCodec, int outputSampleRate)
    {
        try
        {
            GetFFmpegFormat(inputCodec);
            GetFFmpegFormat(outputCodec);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public async Task<byte[]> ConvertAsync(
        byte[] inputAudioBytes, AudioCodec inputCodec, int inputSampleRate, AudioCodec outputCodec, int outputSampleRate, CancellationToken cancellationToken)
    {
        if (!IsConversionSupported(inputCodec, inputSampleRate, outputCodec, outputSampleRate))
            throw new NotSupportedException($"不支持从 {inputCodec} @ {inputSampleRate}Hz 到 {outputCodec} @ {outputSampleRate}Hz 的音频转换。");
        
        var inputFormat = GetFFmpegFormat(inputCodec);
        var outputFormat = GetFFmpegFormat(outputCodec);
        
        return await _ffmpegService.ConvertAsync(inputAudioBytes, inputFormat, inputSampleRate, outputFormat, outputSampleRate, cancellationToken).ConfigureAwait(false);
    }
    
    private static string GetFFmpegFormat(AudioCodec codec)
    {
        return codec switch
        {
            AudioCodec.MULAW => "mulaw",
            AudioCodec.ALAW => "alaw",
            AudioCodec.PCM16 => "s16le",
            AudioCodec.OPUS => "opus",
            _ => throw new NotSupportedException($"不支持的音频编解码器: {codec}")
        };
    }
}