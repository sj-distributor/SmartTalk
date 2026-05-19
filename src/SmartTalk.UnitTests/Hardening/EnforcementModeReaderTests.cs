using Shouldly;
using SmartTalk.Messages.Hardening;
using Xunit;

namespace SmartTalk.UnitTests.Hardening;

/// <summary>
/// Pins the parsing contract of <see cref="EnforcementModeReader"/>. Operators rely on a
/// stable set of aliases (case-insensitive, with common synonyms) to drive runtime
/// behaviour from a single env var. Any silent change here would force every air-gapped
/// operator to learn the new vocabulary.
/// </summary>
public class EnforcementModeReaderTests
{
    [Theory]
    [InlineData("off",       EnforcementMode.Off)]
    [InlineData("Off",       EnforcementMode.Off)]
    [InlineData("OFF",       EnforcementMode.Off)]
    [InlineData("disabled",  EnforcementMode.Off)]
    [InlineData("0",         EnforcementMode.Off)]
    [InlineData("false",     EnforcementMode.Off)]
    [InlineData("no",        EnforcementMode.Off)]
    [InlineData("warn",      EnforcementMode.Warn)]
    [InlineData("Warn",      EnforcementMode.Warn)]
    [InlineData("warning",   EnforcementMode.Warn)]
    [InlineData("strict",    EnforcementMode.Strict)]
    [InlineData("Strict",    EnforcementMode.Strict)]
    [InlineData("enforce",   EnforcementMode.Strict)]
    [InlineData("1",         EnforcementMode.Strict)]
    [InlineData("true",      EnforcementMode.Strict)]
    [InlineData("yes",       EnforcementMode.Strict)]
    public void Parse_RecognisedAlias_MapsToExpectedMode(string raw, EnforcementMode expected)
    {
        EnforcementModeReader.Parse(raw).ShouldBe(expected);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    [InlineData("WARN ")]                  // trailing whitespace — Parse expects pre-trimmed input
    [InlineData("on")]                     // ambiguous — not aliased
    public void Parse_UnrecognisedValue_FallsBackToDefault(string raw)
    {
        EnforcementModeReader.Parse(raw, defaultMode: EnforcementMode.Warn).ShouldBe(EnforcementMode.Warn);
        EnforcementModeReader.Parse(raw, defaultMode: EnforcementMode.Off).ShouldBe(EnforcementMode.Off);
        EnforcementModeReader.Parse(raw, defaultMode: EnforcementMode.Strict).ShouldBe(EnforcementMode.Strict);
    }

    [Fact]
    public void Read_EnvVarUnset_ReturnsDefault()
    {
        // Use a unique env var name so concurrent tests do not collide.
        var envVarName = $"SQUID_TEST_ENFORCEMENT_UNSET_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(envVarName, null);

        EnforcementModeReader.Read(envVarName, EnforcementMode.Warn).ShouldBe(EnforcementMode.Warn);
        EnforcementModeReader.Read(envVarName, EnforcementMode.Off).ShouldBe(EnforcementMode.Off);
    }

    [Fact]
    public void Read_EnvVarSetToWhitespace_ReturnsDefault()
    {
        var envVarName = $"SQUID_TEST_ENFORCEMENT_WHITESPACE_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(envVarName, "   ");

        try
        {
            EnforcementModeReader.Read(envVarName, EnforcementMode.Warn).ShouldBe(EnforcementMode.Warn);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Theory]
    [InlineData("off",    EnforcementMode.Off)]
    [InlineData(" warn ", EnforcementMode.Warn)]   // trimming
    [InlineData("STRICT", EnforcementMode.Strict)] // case-insensitive
    public void Read_EnvVarSetToAlias_TrimsAndParses(string envValue, EnforcementMode expected)
    {
        var envVarName = $"SQUID_TEST_ENFORCEMENT_ALIAS_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(envVarName, envValue);

        try
        {
            EnforcementModeReader.Read(envVarName, defaultMode: EnforcementMode.Off).ShouldBe(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }
}
