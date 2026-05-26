using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using Xunit;

namespace SmartTalk.UnitTests.Services.AiSpeechAssistantConnect;

/// <summary>
/// Exercises every input shape <see cref="AiSpeechAssistantConnectService.ParseEnableRealtimeTracing"/>
/// might receive from the deserialised <c>ai_speech_assistant_function_call.content</c> JSON.
///
/// <para>
/// The single load-bearing invariant: anything that should leave tracing OFF
/// (current behaviour — OpenAI does NOT retain a session trace) MUST return
/// <c>null</c>. Only an explicit <c>{ "enabled": true }</c> activates tracing.
/// An explicit <c>{ "enabled": false }</c> also returns null — operators can
/// persist the row in the inactive state without accidentally enabling tracing,
/// distinguishing "deliberately off" from "no row" semantically.
/// </para>
/// </summary>
public class ParseEnableRealtimeTracingTests
{
    private static object DeserializeContent(string json) => JsonConvert.DeserializeObject<object>(json);

    // ── Default-path inputs (every one of these must return null) ──────────

    [Fact]
    public void ParseEnableRealtimeTracing_NullInput_ReturnsNull()
    {
        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing(null).ShouldBeNull();
    }

    [Fact]
    public void ParseEnableRealtimeTracing_NonJObjectInput_ReturnsNull()
    {
        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing(JArray.Parse("[true]")).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing(true).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing("true").ShouldBeNull();
    }

    [Fact]
    public void ParseEnableRealtimeTracing_EmptyJsonObject_ReturnsNull()
    {
        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing(DeserializeContent("{}")).ShouldBeNull();
    }

    [Fact]
    public void ParseEnableRealtimeTracing_EnabledPropertyExplicitNull_ReturnsNull()
    {
        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing(DeserializeContent("{\"enabled\":null}")).ShouldBeNull();
    }

    [Theory]
    [InlineData("{\"enabled\":\"true\"}")]    // string instead of bool
    [InlineData("{\"enabled\":1}")]            // integer
    [InlineData("{\"enabled\":[true]}")]       // array
    public void ParseEnableRealtimeTracing_NonBooleanValue_ReturnsNull(string content)
    {
        // The schema requires a real JSON boolean. Anything else (string-coercible
        // truthy values, numbers, arrays) is treated as malformed → tracing stays off.
        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing(DeserializeContent(content)).ShouldBeNull();
    }

    [Fact]
    public void ParseEnableRealtimeTracing_ExplicitFalse_ReturnsNull()
    {
        // An explicit `{ "enabled": false }` MUST behave identically to a missing row
        // or an inactive row. Operators sometimes persist the "off" state explicitly
        // (e.g. via UI checkbox that defaults to false); we must not interpret that
        // as a tracing opt-in.
        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing(DeserializeContent("{\"enabled\":false}")).ShouldBeNull();
    }

    // ── Active opt-in inputs ───────────────────────────────────────────────

    [Fact]
    public void ParseEnableRealtimeTracing_ExplicitTrue_ReturnsTrue()
    {
        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing(DeserializeContent("{\"enabled\":true}")).ShouldBe(true);
    }

    [Fact]
    public void ParseEnableRealtimeTracing_AdditionalProperties_StillExtractsEnabled()
    {
        // Forward-compatibility for future schema additions (e.g. workflow_name,
        // group_id for finer-grained OpenAI trace classification). Extras are
        // ignored; the parser only reads `enabled`.
        var input = DeserializeContent("{\"enabled\":true,\"workflow_name\":\"escalation_123\",\"group_id\":\"ops\"}");

        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing(input).ShouldBe(true);
    }

    [Fact]
    public void ParseEnableRealtimeTracing_EnabledKeyIsCaseSensitive()
    {
        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing(DeserializeContent("{\"Enabled\":true}")).ShouldBeNull();
        AiSpeechAssistantConnectService.ParseEnableRealtimeTracing(DeserializeContent("{\"ENABLED\":true}")).ShouldBeNull();
    }
}
