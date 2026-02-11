using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters;

public interface IRealtimeAiAudioCodecAdapter : IScopedDependency
{
    bool IsConversionSupported(RealtimeAiAudioCodec inputCodec, int inputSampleRate, RealtimeAiAudioCodec outputCodec, int outputSampleRate);
    
    Task<byte[]> ConvertAsync(byte[] inputAudioBytes, RealtimeAiAudioCodec inputCodec, int inputSampleRate, RealtimeAiAudioCodec outputCodec, int outputSampleRate, CancellationToken cancellationToken);
}