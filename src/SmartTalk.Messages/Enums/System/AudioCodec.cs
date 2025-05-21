namespace SmartTalk.Messages.Enums.System;

public enum AudioCodec
{
    /// <summary>
    /// G.711 µ-law (通常用于北美电话系统, 8kHz)
    /// G.711 µ-law (Common in North American telephony, 8kHz)
    /// </summary>
    MULAW,
    /// <summary>
    /// G.711 A-law (通常用于欧洲等地区电话系统, 8kHz)
    /// G.711 A-law (Common in telephony outside North America, 8kHz)
    /// </summary>
    ALAW,
    /// <summary>
    /// 脉冲编码调制, 16位线性采样 (小端)
    /// Pulse-code modulation, 16-bit linear samples (Little Endian)
    /// </summary>
    PCM16, // 对应 FFmpeg 的 s16le (Corresponds to FFmpeg's s16le)
    /// <summary>
    /// Opus 编解码器 (高效的有损格式)
    /// Opus codec (efficient lossy format)
    /// </summary>
    OPUS
}