using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.RealtimeAi.wss;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.Tools;

public interface IRealtimeAiAudioCodecAdapter : IScopedDependency, IRealtimeAiProvider
{
    bool IsConversionSupported(RealtimeAiAudioCodec inputCodec, int inputSampleRate, RealtimeAiAudioCodec outputCodec, int outputSampleRate);
    
    Task<byte[]> ConvertAsync(byte[] inputAudioBytes, RealtimeAiAudioCodec inputCodec, int inputSampleRate, RealtimeAiAudioCodec outputCodec, int outputSampleRate, CancellationToken cancellationToken);
}