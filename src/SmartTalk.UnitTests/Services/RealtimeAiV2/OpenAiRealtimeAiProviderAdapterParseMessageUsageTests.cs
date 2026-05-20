using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// End-to-end tests that the V2 adapter's <see cref="OpenAiRealtimeAiProviderAdapter.ParseMessage"/>
/// surfaces the usage block on the parsed event for the two <c>response.done</c> variants
/// (<see cref="RealtimeAiWssEventType.ResponseTurnCompleted"/> and
/// <see cref="RealtimeAiWssEventType.FunctionCallSuggested"/>).
///
/// <para>
/// The unit-test counterpart of <see cref="OpenAiRealtimeAiProviderAdapterExtractUsageTests"/> —
/// those exercise the parser in isolation; these exercise the wiring that puts the
/// parsed value onto the event consumers read.
/// </para>
/// </summary>
public class OpenAiRealtimeAiProviderAdapterParseMessageUsageTests
{
    private static OpenAiRealtimeAiProviderAdapter NewAdapter() =>
        new(new OpenAiSettings(OpenAiRealtimeAiProviderAdapterTestSettings.BuildConfiguration()));

    [Fact]
    public void ParseMessage_ResponseDoneWithUsage_AttachesUsageToCompletionEvent()
    {
        var raw = """
            {
              "type": "response.done",
              "response": {
                "id": "resp_test",
                "usage": {
                  "total_tokens": 1234,
                  "input_tokens": 800,
                  "output_tokens": 434,
                  "input_token_details": { "cached_tokens": 200 }
                }
              }
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseTurnCompleted);
        parsed.Usage.ShouldNotBeNull();
        parsed.Usage.TotalTokens.ShouldBe(1234);
        parsed.Usage.InputTokens.ShouldBe(800);
        parsed.Usage.OutputTokens.ShouldBe(434);
        parsed.Usage.CachedTokens.ShouldBe(200);
    }

    [Fact]
    public void ParseMessage_ResponseDoneWithoutUsage_LeavesUsageNull()
    {
        // Older provider snapshots emit response.done with no usage block. The
        // event still surfaces (cleanup logic depends on it) but Usage is null
        // and the consumer's usage callback is skipped.
        var raw = """
            {
              "type": "response.done",
              "response": {
                "id": "resp_test"
              }
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseTurnCompleted);
        parsed.Usage.ShouldBeNull();
    }

    [Fact]
    public void ParseMessage_ResponseDoneWithFunctionCallAndUsage_AttachesUsageToFunctionCallEvent()
    {
        // The same response.done message can be classified as
        // FunctionCallSuggested when the response output contains function calls.
        // Usage MUST still be surfaced — operators need cost tracking on
        // function-call turns just as much as on text turns.
        var raw = """
            {
              "type": "response.done",
              "response": {
                "id": "resp_test",
                "output": [
                  {
                    "type": "function_call",
                    "name": "confirm_order",
                    "call_id": "call_abc",
                    "arguments": "{}"
                  }
                ],
                "usage": {
                  "total_tokens": 500,
                  "input_tokens": 480,
                  "output_tokens": 20
                }
              }
            }
            """;

        var parsed = NewAdapter().ParseMessage(raw);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.FunctionCallSuggested);
        parsed.Data.ShouldNotBeNull();   // function call payload still there
        parsed.Usage.ShouldNotBeNull();
        parsed.Usage.TotalTokens.ShouldBe(500);
    }
}
