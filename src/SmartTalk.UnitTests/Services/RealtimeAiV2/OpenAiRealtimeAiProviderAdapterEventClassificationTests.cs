using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using Xunit;

namespace SmartTalk.UnitTests.Services.RealtimeAiV2;

/// <summary>
/// Three arms of the GA event switch fall through to a blank-text branch. The engine logs
/// <c>Unknown</c> at Warning, which is correct for an event name nobody recognises — but two of these
/// arms produce blank text on completely ordinary traffic, so a normal order call fills Seq with
/// "Unknown provider event" Warnings and the level stops being worth alerting on.
///
/// <para>The split matters and is what these tests pin. An <c>output_item.done</c> for a
/// <c>function_call</c> item has no <c>content</c> array by design, so it is benign every time. An
/// <c>output_item.done</c> for a <c>message</c> item that yielded no words is NOT benign — that is the
/// shape a provider wire-format change takes, and it is the tripwire commit ad05df0aa's comment says
/// must stay visible. One enum value cannot mean both.</para>
/// </summary>
public class OpenAiRealtimeAiProviderAdapterEventClassificationTests
{
    private static ParsedRealtimeAiProviderEvent Parse(string json) =>
        new OpenAiRealtimeAiProviderAdapter(new OpenAiSettings(Substitute.For<IConfiguration>())).ParseMessage(json);

    [Fact]
    public void OutputItemDoneForAFunctionCall_ShouldBeIgnoredRatherThanUnknown()
    {
        // Every tool call produces this frame. A function_call item carries type/name/arguments/call_id
        // and no content array, so the text extraction can only ever come back blank. On an assistant
        // with seventeen tools this was one bogus Warning per tool call, for the length of the call.
        var parsed = Parse("""
            {"type":"response.output_item.done","item":{"id":"item_1","type":"function_call","name":"order","call_id":"call_1","arguments":"{}"}}
            """);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.Ignored,
            "a tool call is ordinary traffic, not an event the adapter failed to recognise");
    }

    [Fact]
    public void ContentPartAddedBeforeItHoldsATranscript_ShouldBeIgnoredRatherThanUnknown()
    {
        // An audio part is announced empty and filled later — that is what content_part.added is for,
        // so it is blank once per turn on the built-in audio path.
        var parsed = Parse("""
            {"type":"response.content_part.added","item_id":"item_1","part":{"type":"audio","transcript":""}}
            """);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.Ignored);
    }

    [Fact]
    public void OutputItemDoneForAMessageThatProducedNoWords_ShouldStayUnknown()
    {
        // The tripwire. An assistant message that finished with nothing in it is not routine, and this
        // is the classification a wire-format change would surface through. Losing it to the same
        // Ignored bucket as a tool call is how the fix above turns into a silent regression detector.
        var parsed = Parse("""
            {"type":"response.output_item.done","item":{"id":"item_1","type":"message","content":[]}}
            """);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.Unknown,
            "a message with no words is the shape a provider regression takes, and must stay visible");
    }

    [Fact]
    public void OutputItemDoneWithNoReadableItem_ShouldStayUnknown()
    {
        // The classification reads item.type. A frame that does not carry one is a shape the adapter
        // did not expect, which is precisely what Unknown exists to report — so the split must fail
        // toward visible, not toward silent.
        var parsed = Parse("""
            {"type":"response.output_item.done"}
            """);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.Unknown);
    }

    [Fact]
    public void OutputItemDoneCarryingText_ShouldStillReportResponseTextDone()
    {
        // The non-blank arm was never pinned, and it is the caller-audible one: it drives
        // FlushProviderTextToTtsAsync, i.e. the words a caller hears on the external-TTS path. Pinned
        // here as a green guard so narrowing the blank arm cannot quietly reroute the loud one.
        var parsed = Parse("""
            {"type":"response.output_item.done","item":{"id":"item_1","type":"message","content":[{"type":"output_text","text":"your order is ready"}]}}
            """);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.ResponseTextDone);
        ((RealtimeAiWssTextData)parsed.Data).Text.ShouldBe("your order is ready");
    }

    [Fact]
    public void ContentPartDoneWithNoText_ShouldStayUnknown()
    {
        // Deliberately NOT changed alongside its sibling. A part that is DONE and still empty is not
        // the same claim as one that was merely announced empty, and nothing observed says it is
        // routine — so it keeps the tripwire until there is evidence to spend.
        var parsed = Parse("""
            {"type":"response.content_part.done","item_id":"item_1","part":{"type":"audio","transcript":""}}
            """);

        parsed.Type.ShouldBe(RealtimeAiWssEventType.Unknown);
    }
}
