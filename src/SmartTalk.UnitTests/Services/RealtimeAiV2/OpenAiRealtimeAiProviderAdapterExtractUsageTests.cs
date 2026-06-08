using System.Text.Json;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Exercises <see cref="OpenAiRealtimeAiProviderAdapter.ExtractUsage"/> against the
/// shapes of OpenAI's <c>response.done.response.usage</c> payload. The parser must
/// be defensive: older provider snapshots omit the usage block entirely, and even
/// modern responses may omit individual sub-counts.
///
/// <para>
/// The load-bearing invariant: a missing block or a missing sub-count returns
/// <c>null</c> for that level, never zero. Zero is a meaningful value (an empty
/// AI turn) and conflating it with "missing" would silently corrupt cost reports.
/// </para>
/// </summary>
public class OpenAiRealtimeAiProviderAdapterExtractUsageTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    // ── Missing / wrong-shape returns null ────────────────────────────────

    [Fact]
    public void ExtractUsage_NoResponseObject_ReturnsNull()
    {
        // Older provider snapshots emit `response.done` with just `{ type: "response.done" }`.
        // Parser must return null rather than throw.
        OpenAiRealtimeAiProviderAdapter.ExtractUsage(Parse("{\"type\":\"response.done\"}")).ShouldBeNull();
    }

    [Fact]
    public void ExtractUsage_ResponseObjectButNoUsage_ReturnsNull()
    {
        // response is present but doesn't carry usage — that's not an error,
        // it just means OpenAI didn't report token counts for this turn.
        var root = Parse("{\"type\":\"response.done\",\"response\":{\"id\":\"resp_123\"}}");

        OpenAiRealtimeAiProviderAdapter.ExtractUsage(root).ShouldBeNull();
    }

    [Fact]
    public void ExtractUsage_ResponseNotAnObject_ReturnsNull()
    {
        // Defensive against malformed events where `response` is null or a scalar.
        var root = Parse("{\"type\":\"response.done\",\"response\":null}");

        OpenAiRealtimeAiProviderAdapter.ExtractUsage(root).ShouldBeNull();
    }

    [Fact]
    public void ExtractUsage_UsageNotAnObject_ReturnsNull()
    {
        var root = Parse("{\"type\":\"response.done\",\"response\":{\"usage\":\"unexpected\"}}");

        OpenAiRealtimeAiProviderAdapter.ExtractUsage(root).ShouldBeNull();
    }

    // ── Top-level sub-counts ──────────────────────────────────────────────

    [Fact]
    public void ExtractUsage_OnlyTopLevelCounts_ReturnsTotalInputOutput()
    {
        // The most minimal usage block — just the three top-level sums.
        var root = Parse("""
            {
              "type": "response.done",
              "response": {
                "usage": {
                  "total_tokens": 1500,
                  "input_tokens": 800,
                  "output_tokens": 700
                }
              }
            }
            """);

        var usage = OpenAiRealtimeAiProviderAdapter.ExtractUsage(root);

        usage.ShouldNotBeNull();
        usage.TotalTokens.ShouldBe(1500);
        usage.InputTokens.ShouldBe(800);
        usage.OutputTokens.ShouldBe(700);
        usage.CachedTokens.ShouldBeNull();
        usage.InputAudioTokens.ShouldBeNull();
        usage.InputTextTokens.ShouldBeNull();
        usage.OutputAudioTokens.ShouldBeNull();
        usage.OutputTextTokens.ShouldBeNull();
    }

    [Fact]
    public void ExtractUsage_TopLevelMissingTotal_ReturnsNullForThatField()
    {
        // Missing total_tokens is not an error — older snapshots sometimes omit it
        // when the breakdown sums are sufficient. Other fields must still populate.
        var root = Parse("""
            {
              "response": {
                "usage": {
                  "input_tokens": 100,
                  "output_tokens": 50
                }
              }
            }
            """);

        var usage = OpenAiRealtimeAiProviderAdapter.ExtractUsage(root);

        usage.TotalTokens.ShouldBeNull();
        usage.InputTokens.ShouldBe(100);
        usage.OutputTokens.ShouldBe(50);
    }

    [Fact]
    public void ExtractUsage_TopLevelZeroValues_PreservedAsZeroNotNull()
    {
        // Zero is a real value (empty AI turn). MUST be preserved as 0 — collapsing it
        // to null would silently corrupt cost reports for empty / cancelled turns.
        var root = Parse("{\"response\":{\"usage\":{\"total_tokens\":0,\"input_tokens\":0,\"output_tokens\":0}}}");

        var usage = OpenAiRealtimeAiProviderAdapter.ExtractUsage(root);

        usage.TotalTokens.ShouldBe(0);
        usage.InputTokens.ShouldBe(0);
        usage.OutputTokens.ShouldBe(0);
    }

    // ── Nested input_token_details / output_token_details ─────────────────

    [Fact]
    public void ExtractUsage_FullBreakdown_ReturnsAllFields()
    {
        var root = Parse("""
            {
              "response": {
                "usage": {
                  "total_tokens": 2000,
                  "input_tokens": 1200,
                  "output_tokens": 800,
                  "input_token_details": {
                    "cached_tokens": 400,
                    "audio_tokens": 500,
                    "text_tokens": 300
                  },
                  "output_token_details": {
                    "audio_tokens": 600,
                    "text_tokens": 200
                  }
                }
              }
            }
            """);

        var usage = OpenAiRealtimeAiProviderAdapter.ExtractUsage(root);

        usage.TotalTokens.ShouldBe(2000);
        usage.InputTokens.ShouldBe(1200);
        usage.OutputTokens.ShouldBe(800);
        usage.CachedTokens.ShouldBe(400);
        usage.InputAudioTokens.ShouldBe(500);
        usage.InputTextTokens.ShouldBe(300);
        usage.OutputAudioTokens.ShouldBe(600);
        usage.OutputTextTokens.ShouldBe(200);
    }

    [Fact]
    public void ExtractUsage_InputTokenDetailsAbsent_OutputDetailsStillExtracted()
    {
        // Provider may report one side and not the other. Each nested block is
        // optional independently of the other.
        var root = Parse("""
            {
              "response": {
                "usage": {
                  "input_tokens": 100,
                  "output_tokens": 50,
                  "output_token_details": {
                    "audio_tokens": 40,
                    "text_tokens": 10
                  }
                }
              }
            }
            """);

        var usage = OpenAiRealtimeAiProviderAdapter.ExtractUsage(root);

        usage.CachedTokens.ShouldBeNull();
        usage.InputAudioTokens.ShouldBeNull();
        usage.InputTextTokens.ShouldBeNull();
        usage.OutputAudioTokens.ShouldBe(40);
        usage.OutputTextTokens.ShouldBe(10);
    }

    [Fact]
    public void ExtractUsage_NestedDetailsAreNull_ReturnsNullForLeafFields()
    {
        // Defensive: the provider may return null instead of an object for nested details.
        var root = Parse("""
            {
              "response": {
                "usage": {
                  "input_tokens": 100,
                  "output_tokens": 50,
                  "input_token_details": null,
                  "output_token_details": null
                }
              }
            }
            """);

        var usage = OpenAiRealtimeAiProviderAdapter.ExtractUsage(root);

        usage.CachedTokens.ShouldBeNull();
        usage.InputAudioTokens.ShouldBeNull();
        usage.InputTextTokens.ShouldBeNull();
        usage.OutputAudioTokens.ShouldBeNull();
        usage.OutputTextTokens.ShouldBeNull();
    }

    [Fact]
    public void ExtractUsage_NonIntegerCounts_ReturnsNullForThoseFields()
    {
        // Schema violation: a count is a string or a float. The parser tolerates
        // this by returning null for the affected leaf rather than throwing.
        var root = Parse("""
            {
              "response": {
                "usage": {
                  "total_tokens": "1500",
                  "input_tokens": 100.5,
                  "output_tokens": 50
                }
              }
            }
            """);

        var usage = OpenAiRealtimeAiProviderAdapter.ExtractUsage(root);

        usage.TotalTokens.ShouldBeNull();
        usage.InputTokens.ShouldBeNull();
        usage.OutputTokens.ShouldBe(50);
    }

    [Fact]
    public void ExtractUsage_UnknownExtraFields_AreSilentlyIgnored()
    {
        // Forward-compatibility: when OpenAI adds new sub-counts (e.g. a new audio
        // category), older builds of the parser must keep working. Extras are
        // silently ignored; the documented fields still extract correctly.
        var root = Parse("""
            {
              "response": {
                "usage": {
                  "total_tokens": 100,
                  "input_tokens": 50,
                  "output_tokens": 50,
                  "future_premium_tokens": 999,
                  "input_token_details": {
                    "cached_tokens": 10,
                    "exotic_new_category_tokens": 20
                  }
                }
              }
            }
            """);

        var usage = OpenAiRealtimeAiProviderAdapter.ExtractUsage(root);

        usage.TotalTokens.ShouldBe(100);
        usage.CachedTokens.ShouldBe(10);
    }
}
