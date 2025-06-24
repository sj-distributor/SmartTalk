using System.ComponentModel;

namespace SmartTalk.Messages.Enums.RealtimeAi;

public enum RealtimeAiAudioCodec
{
    [Description("g711_ulaw")]
    MULAW,
    
    [Description("g712_alaw")]
    ALAW,
    
    [Description("pcm16")]
    PCM16,
    
    [Description("image")]
    IMAGE
}