using System.Text;
using System.Text.Json;
using Serilog;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.MiniMax;

public static class MiniMaxRealtimeAiTtsPayloadParser
{
    public static bool TryGetAudioSampleRate(JsonElement root, out int audioSampleRate)
    {
        audioSampleRate = 0;

        if (root.TryGetProperty("extra_info", out var extraInfo) && extraInfo.ValueKind == JsonValueKind.Object)
        {
            if (TryReadIntProperty(extraInfo, "audio_sample_rate", out audioSampleRate))
                return true;
        }

        return TryReadIntProperty(root, "audio_sample_rate", out audioSampleRate);
    }

    public static bool TryExtractWavPcm16(byte[] audioBytes, out int sampleRate, out byte[] pcmBytes)
    {
        sampleRate = 0;
        pcmBytes = Array.Empty<byte>();

        if (audioBytes.Length < 44) return false;
        if (!(audioBytes[0] == 'R' && audioBytes[1] == 'I' && audioBytes[2] == 'F' && audioBytes[3] == 'F')) return false;
        if (!(audioBytes[8] == 'W' && audioBytes[9] == 'A' && audioBytes[10] == 'V' && audioBytes[11] == 'E')) return false;

        var offset = 12;
        var foundFmt = false;
        var foundData = false;
        var dataOffset = 0;
        var dataSize = 0;
        short channels = 1;
        short bitsPerSample = 16;

        while (offset + 8 <= audioBytes.Length)
        {
            var chunkId = Encoding.ASCII.GetString(audioBytes, offset, 4);
            var chunkSize = BitConverter.ToInt32(audioBytes, offset + 4);
            offset += 8;

            if (chunkSize < 0 || offset + chunkSize > audioBytes.Length)
                break;

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                var audioFormat = BitConverter.ToInt16(audioBytes, offset);
                channels = BitConverter.ToInt16(audioBytes, offset + 2);
                sampleRate = BitConverter.ToInt32(audioBytes, offset + 4);
                bitsPerSample = BitConverter.ToInt16(audioBytes, offset + 14);
                foundFmt = audioFormat == 1;
            }
            else if (chunkId == "data")
            {
                dataOffset = offset;
                dataSize = chunkSize;
                foundData = true;
            }

            offset += chunkSize;
            if ((chunkSize & 1) == 1) offset += 1;
        }

        if (!foundFmt || !foundData || dataSize <= 0 || sampleRate <= 0)
            return false;

        if (channels != 1 || bitsPerSample != 16)
            return false;

        pcmBytes = new byte[dataSize];
        Buffer.BlockCopy(audioBytes, dataOffset, pcmBytes, 0, dataSize);

        return true;
    }

    public static bool TryGetAudioPayload(JsonElement root, out byte[] audioBytes)
    {
        audioBytes = Array.Empty<byte>();

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return false;

        if (!data.TryGetProperty("audio", out var audio) || audio.ValueKind != JsonValueKind.String)
            return false;

        var payload = audio.GetString();
        if (string.IsNullOrWhiteSpace(payload)) return false;

        if (IsHexPayload(payload))
        {
            try
            {
                audioBytes = Convert.FromHexString(payload);
                return audioBytes.Length > 0;
            }
            catch
            {
                // Try base64 below.
            }
        }

        try
        {
            audioBytes = Convert.FromBase64String(payload);
            return audioBytes.Length > 0;
        }
        catch
        {
            Log.Warning("[RealtimeAi][MiniMaxTts] Unsupported audio payload encoding.");
            return false;
        }
    }

    private static bool TryReadIntProperty(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var prop)) return false;

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value))
            return true;

        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value))
            return true;

        return false;
    }

    private static bool IsHexPayload(string payload)
    {
        if ((payload.Length & 1) != 0) return false;

        foreach (var ch in payload)
        {
            var isHex = ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!isHex) return false;
        }

        return true;
    }
}
