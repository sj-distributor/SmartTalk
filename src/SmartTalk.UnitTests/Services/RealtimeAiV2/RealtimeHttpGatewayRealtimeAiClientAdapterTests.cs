using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Clients.RealtimeHttpGateway;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class RealtimeHttpGatewayRealtimeAiClientAdapterTests
{
    private static RealtimeHttpGatewayRealtimeAiClientAdapter NewAdapter() => new();

    [Fact]
    public void ParseMessage_TextInput_ReturnsText()
    {
        var parsed = NewAdapter().ParseMessage("""{"type":"RealtimeHttpTextInput","text":"hello"}""");

        parsed.Type.ShouldBe(RealtimeAiClientMessageType.Text);
        parsed.Payload.ShouldBe("hello");
    }

    [Fact]
    public void ParseMessage_RecordingAudio_ReturnsRecordingAudio()
    {
        var parsed = NewAdapter().ParseMessage("""{"type":"RealtimeHttpRecordingAudio","payload":"AQIDBA=="}""");

        parsed.Type.ShouldBe(RealtimeAiClientMessageType.RecordingAudio);
        parsed.Payload.ShouldBe("AQIDBA==");
    }

    [Fact]
    public void ParseMessage_DefaultMediaShape_ReturnsUnknown()
    {
        var parsed = NewAdapter().ParseMessage("""{"media":{"payload":"AQIDBA=="}}""");

        parsed.Type.ShouldBe(RealtimeAiClientMessageType.Unknown);
    }
}
