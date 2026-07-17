using System.Text;
using System.Text.Json;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.MiniMax;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class MiniMaxRealtimeAiTtsPayloadParserTests
{
    [Fact]
    public void TryGetAudioPayload_HexPayload_DecodesBytes()
    {
        using var doc = JsonDocument.Parse("""{"data":{"audio":"0102ff"}}""");

        MiniMaxRealtimeAiTtsPayloadParser.TryGetAudioPayload(doc.RootElement, out var bytes).ShouldBeTrue();

        bytes.ShouldBe(new byte[] { 0x01, 0x02, 0xff });
    }

    [Fact]
    public void TryGetAudioPayload_Base64Payload_DecodesBytes()
    {
        using var doc = JsonDocument.Parse("""{"data":{"audio":"AQID"}}""");

        MiniMaxRealtimeAiTtsPayloadParser.TryGetAudioPayload(doc.RootElement, out var bytes).ShouldBeTrue();

        bytes.ShouldBe(new byte[] { 0x01, 0x02, 0x03 });
    }

    [Fact]
    public void TryGetAudioSampleRate_ReadsExtraInfoStringValue()
    {
        using var doc = JsonDocument.Parse("""{"extra_info":{"audio_sample_rate":"16000"}}""");

        MiniMaxRealtimeAiTtsPayloadParser.TryGetAudioSampleRate(doc.RootElement, out var sampleRate).ShouldBeTrue();

        sampleRate.ShouldBe(16000);
    }

    [Fact]
    public void TryExtractWavPcm16_StripsWavHeaderAndReturnsSampleRate()
    {
        var pcm = new byte[] { 0x01, 0x00, 0xff, 0x7f };
        var wav = BuildMonoPcm16Wav(pcm, 24000);

        MiniMaxRealtimeAiTtsPayloadParser.TryExtractWavPcm16(wav, out var sampleRate, out var extractedPcm).ShouldBeTrue();

        sampleRate.ShouldBe(24000);
        extractedPcm.ShouldBe(pcm);
    }

    private static byte[] BuildMonoPcm16Wav(byte[] pcm, int sampleRate)
    {
        using var stream = new MemoryStream();

        WriteAscii(stream, "RIFF");
        WriteInt32(stream, 36 + pcm.Length);
        WriteAscii(stream, "WAVE");
        WriteAscii(stream, "fmt ");
        WriteInt32(stream, 16);
        WriteInt16(stream, 1);
        WriteInt16(stream, 1);
        WriteInt32(stream, sampleRate);
        WriteInt32(stream, sampleRate * 2);
        WriteInt16(stream, 2);
        WriteInt16(stream, 16);
        WriteAscii(stream, "data");
        WriteInt32(stream, pcm.Length);
        stream.Write(pcm);

        return stream.ToArray();
    }

    private static void WriteAscii(Stream stream, string text)
    {
        stream.Write(Encoding.ASCII.GetBytes(text));
    }

    private static void WriteInt16(Stream stream, short value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }

    private static void WriteInt32(Stream stream, int value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
}
