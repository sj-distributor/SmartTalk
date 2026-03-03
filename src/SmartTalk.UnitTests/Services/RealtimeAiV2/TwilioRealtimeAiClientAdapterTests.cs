using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Clients.Twilio;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

public class TwilioRealtimeAiClientAdapterTests
{
    [Fact]
    public void BuildTranscriptionMessage_ShouldReturnNull_ForTwilioStream()
    {
        var sut = new TwilioRealtimeAiClientAdapter();

        var result = sut.BuildTranscriptionMessage(
            RealtimeAiWssEventType.InputAudioTranscriptionCompleted,
            new RealtimeAiWssTranscriptionData { Transcript = "hello" },
            "session-1");

        result.ShouldBeNull();
    }

    [Fact]
    public void BuildErrorMessage_ShouldReturnNull_ForTwilioStream()
    {
        var sut = new TwilioRealtimeAiClientAdapter();

        var result = sut.BuildErrorMessage("x", "y", "session-1");

        result.ShouldBeNull();
    }
}

