using System.ComponentModel;

namespace SmartTalk.Messages.Enums.RealtimeAi;

public enum RealtimeAiAudioCodec
{
    [Description("g711_ulaw")]
    MULAW,
    
    [Description("g711_alaw")]
    ALAW,
    
    [Description("pcm16")]
    PCM16
}