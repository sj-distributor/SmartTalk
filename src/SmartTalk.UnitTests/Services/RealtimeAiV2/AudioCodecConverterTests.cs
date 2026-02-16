using Microsoft.Extensions.Configuration;
using NAudio.Codecs;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class AudioCodecConverterTests
{
    private static readonly short[] TestSamples = { 0, 1000, -1000, short.MaxValue, short.MinValue, 8000, -8000 };

    private static byte[] BuildPcm16(short[] samples)
    {
        var pcm = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            pcm[i * 2] = (byte)(samples[i] & 0xFF);
            pcm[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
        }
        return pcm;
    }

    private static byte[] BuildMulaw(short[] samples) =>
        samples.Select(s => MuLawEncoder.LinearToMuLawSample(s)).ToArray();

    private static byte[] BuildAlaw(short[] samples) =>
        samples.Select(s => ALawEncoder.LinearToALawSample(s)).ToArray();

    // ── Same codec: no conversion ───────────────────────────────

    [Theory]
    [InlineData(RealtimeAiAudioCodec.PCM16)]
    [InlineData(RealtimeAiAudioCodec.MULAW)]
    [InlineData(RealtimeAiAudioCodec.ALAW)]
    public void Convert_SameCodec_ReturnsSameReference(RealtimeAiAudioCodec codec)
    {
        var input = new byte[] { 1, 2, 3, 4 };

        var result = AudioCodecConverter.Convert(input, codec, codec);

        result.ShouldBeSameAs(input);
    }

    // ── MULAW ↔ PCM16 roundtrip ─────────────────────────────────

    [Fact]
    public void Convert_MulawToPcm16_ProducesCorrectLength()
    {
        var mulaw = BuildMulaw(TestSamples);

        var pcm = AudioCodecConverter.Convert(mulaw, RealtimeAiAudioCodec.MULAW, RealtimeAiAudioCodec.PCM16);

        pcm.Length.ShouldBe(mulaw.Length * 2);
    }

    [Fact]
    public void Convert_Pcm16ToMulaw_ProducesCorrectLength()
    {
        var pcm = BuildPcm16(TestSamples);

        var mulaw = AudioCodecConverter.Convert(pcm, RealtimeAiAudioCodec.PCM16, RealtimeAiAudioCodec.MULAW);

        mulaw.Length.ShouldBe(pcm.Length / 2);
    }

    [Fact]
    public void Convert_MulawToPcm16AndBack_Roundtrips()
    {
        var original = BuildMulaw(TestSamples);

        var pcm = AudioCodecConverter.Convert(original, RealtimeAiAudioCodec.MULAW, RealtimeAiAudioCodec.PCM16);
        var roundtripped = AudioCodecConverter.Convert(pcm, RealtimeAiAudioCodec.PCM16, RealtimeAiAudioCodec.MULAW);

        roundtripped.ShouldBe(original);
    }

    // ── ALAW ↔ PCM16 roundtrip ──────────────────────────────────

    [Fact]
    public void Convert_AlawToPcm16_ProducesCorrectLength()
    {
        var alaw = BuildAlaw(TestSamples);

        var pcm = AudioCodecConverter.Convert(alaw, RealtimeAiAudioCodec.ALAW, RealtimeAiAudioCodec.PCM16);

        pcm.Length.ShouldBe(alaw.Length * 2);
    }

    [Fact]
    public void Convert_Pcm16ToAlaw_ProducesCorrectLength()
    {
        var pcm = BuildPcm16(TestSamples);

        var alaw = AudioCodecConverter.Convert(pcm, RealtimeAiAudioCodec.PCM16, RealtimeAiAudioCodec.ALAW);

        alaw.Length.ShouldBe(pcm.Length / 2);
    }

    [Fact]
    public void Convert_AlawToPcm16AndBack_Roundtrips()
    {
        var original = BuildAlaw(TestSamples);

        var pcm = AudioCodecConverter.Convert(original, RealtimeAiAudioCodec.ALAW, RealtimeAiAudioCodec.PCM16);
        var roundtripped = AudioCodecConverter.Convert(pcm, RealtimeAiAudioCodec.PCM16, RealtimeAiAudioCodec.ALAW);

        roundtripped.ShouldBe(original);
    }

    // ── Unsupported conversion ──────────────────────────────────

    [Fact]
    public void Convert_MulawToAlaw_ThrowsNotSupported()
    {
        var mulaw = new byte[] { 0x80, 0x90 };

        Should.Throw<NotSupportedException>(() =>
            AudioCodecConverter.Convert(mulaw, RealtimeAiAudioCodec.MULAW, RealtimeAiAudioCodec.ALAW));
    }

    // ── GetPreferredCodec scenarios ─────────────────────────────

    [Theory]
    [InlineData(RealtimeAiAudioCodec.MULAW)]
    [InlineData(RealtimeAiAudioCodec.PCM16)]
    [InlineData(RealtimeAiAudioCodec.ALAW)]
    public void OpenAi_GetPreferredCodec_AlwaysReturnsClientCodec(RealtimeAiAudioCodec clientCodec)
    {
        var adapter = new Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi.OpenAiRealtimeAiProviderAdapter(
            new Core.Settings.OpenAi.OpenAiSettings(Substitute.For<IConfiguration>()));

        adapter.GetPreferredCodec(clientCodec).ShouldBe(clientCodec);
    }

    [Theory]
    [InlineData(RealtimeAiAudioCodec.MULAW)]
    [InlineData(RealtimeAiAudioCodec.PCM16)]
    [InlineData(RealtimeAiAudioCodec.ALAW)]
    public void Google_GetPreferredCodec_AlwaysReturnsPcm16(RealtimeAiAudioCodec clientCodec)
    {
        var adapter = new Core.Services.RealtimeAiV2.Adapters.Providers.Google.GoogleRealtimeAiProviderAdapter(
            new Core.Settings.Google.GoogleSettings(Substitute.For<IConfiguration>()));

        adapter.GetPreferredCodec(clientCodec).ShouldBe(RealtimeAiAudioCodec.PCM16);
    }

    // ── End-to-end: conversion matrix ───────────────────────────

    [Fact]
    public void TwilioAndOpenAi_NoConversionNeeded()
    {
        var clientCodec = RealtimeAiAudioCodec.MULAW; // Twilio
        var providerCodec = clientCodec; // OpenAI returns clientCodec

        var audio = new byte[] { 0x80, 0x90, 0xA0 };
        var result = AudioCodecConverter.Convert(audio, clientCodec, providerCodec);

        result.ShouldBeSameAs(audio);
    }

    [Fact]
    public void WebAndOpenAi_NoConversionNeeded()
    {
        var clientCodec = RealtimeAiAudioCodec.PCM16; // Web
        var providerCodec = clientCodec; // OpenAI returns clientCodec

        var audio = new byte[] { 0x00, 0x10, 0x20, 0x30 };
        var result = AudioCodecConverter.Convert(audio, clientCodec, providerCodec);

        result.ShouldBeSameAs(audio);
    }

    [Fact]
    public void WebAndGoogle_NoConversionNeeded()
    {
        var clientCodec = RealtimeAiAudioCodec.PCM16; // Web
        var providerCodec = RealtimeAiAudioCodec.PCM16; // Google always PCM16

        var audio = new byte[] { 0x00, 0x10, 0x20, 0x30 };
        var result = AudioCodecConverter.Convert(audio, clientCodec, providerCodec);

        result.ShouldBeSameAs(audio);
    }

    [Fact]
    public void TwilioAndGoogle_InputConverted_MulawToPcm16()
    {
        var clientCodec = RealtimeAiAudioCodec.MULAW; // Twilio
        var providerCodec = RealtimeAiAudioCodec.PCM16; // Google

        var mulawAudio = BuildMulaw(TestSamples);
        var result = AudioCodecConverter.Convert(mulawAudio, clientCodec, providerCodec);

        result.Length.ShouldBe(mulawAudio.Length * 2);
        result.ShouldNotBe(mulawAudio);
    }

    [Fact]
    public void TwilioAndGoogle_OutputConverted_Pcm16ToMulaw()
    {
        var providerCodec = RealtimeAiAudioCodec.PCM16; // Google
        var clientCodec = RealtimeAiAudioCodec.MULAW; // Twilio

        var pcmAudio = BuildPcm16(TestSamples);
        var result = AudioCodecConverter.Convert(pcmAudio, providerCodec, clientCodec);

        result.Length.ShouldBe(pcmAudio.Length / 2);
        result.ShouldNotBe(pcmAudio);
    }

    // ── GetSampleRate ─────────────────────────────────────────────

    [Theory]
    [InlineData(RealtimeAiAudioCodec.MULAW, 8000)]
    [InlineData(RealtimeAiAudioCodec.ALAW, 8000)]
    [InlineData(RealtimeAiAudioCodec.PCM16, 24000)]
    public void GetSampleRate_ReturnsExpectedRate(RealtimeAiAudioCodec codec, int expectedRate)
    {
        AudioCodecConverter.GetSampleRate(codec).ShouldBe(expectedRate);
    }

    // ── Resample ──────────────────────────────────────────────────

    [Fact]
    public void Resample_SameRate_ReturnsSameReference()
    {
        var pcm = BuildPcm16(TestSamples);

        var result = AudioCodecConverter.Resample(pcm, 24000, 24000);

        result.ShouldBeSameAs(pcm);
    }

    [Fact]
    public void Resample_8kTo24k_TriplesSampleCount()
    {
        var pcm = BuildPcm16(new short[] { 100, 200, 300, 400 }); // 4 samples = 8 bytes

        var result = AudioCodecConverter.Resample(pcm, 8000, 24000);

        // 4 samples * 3 = 12 samples = 24 bytes
        result.Length.ShouldBe(24);
    }

    [Fact]
    public void Resample_24kTo8k_ThirdsSampleCount()
    {
        var pcm = BuildPcm16(new short[] { 100, 200, 300, 400, 500, 600 }); // 6 samples

        var result = AudioCodecConverter.Resample(pcm, 24000, 8000);

        // 6 samples / 3 = 2 samples = 4 bytes
        result.Length.ShouldBe(4);
    }

    // ── ConvertForRecording ───────────────────────────────────────

    [Fact]
    public void ConvertForRecording_Pcm16_NoConversionNoResample()
    {
        var pcm = BuildPcm16(TestSamples);

        var result = AudioCodecConverter.ConvertForRecording(pcm, RealtimeAiAudioCodec.PCM16);

        // PCM16 at 24kHz → same rate, same codec → same reference
        result.ShouldBeSameAs(pcm);
    }

    [Fact]
    public void ConvertForRecording_Mulaw_ConvertedAndResampled()
    {
        var mulaw = new byte[100];
        Array.Fill<byte>(mulaw, 0x80);

        var result = AudioCodecConverter.ConvertForRecording(mulaw, RealtimeAiAudioCodec.MULAW);

        // 100 MULAW → 200 PCM16 (codec) → 600 PCM16 (8kHz→24kHz resample)
        result.Length.ShouldBe(600);
    }

    [Fact]
    public void ConvertForRecording_Alaw_ConvertedAndResampled()
    {
        var alaw = new byte[100];
        Array.Fill<byte>(alaw, 0x80);

        var result = AudioCodecConverter.ConvertForRecording(alaw, RealtimeAiAudioCodec.ALAW);

        // 100 ALAW → 200 PCM16 (codec) → 600 PCM16 (8kHz→24kHz resample)
        result.Length.ShouldBe(600);
    }
}
