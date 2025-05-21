using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.System;

namespace SmartTalk.Core.Services.RealtimeAi.wss.AudioCodecAdapter;

public interface IRealtimeAiAudioCodecAdapter : IScopedDependency
{
    bool IsConversionSupported(AudioCodec inputCodec, int inputSampleRate, AudioCodec outputCodec, int outputSampleRate);
    
    Task<byte[]> ConvertAsync(byte[] inputAudioBytes, AudioCodec inputCodec, int inputSampleRate, AudioCodec outputCodec, int outputSampleRate, CancellationToken cancellationToken);
}