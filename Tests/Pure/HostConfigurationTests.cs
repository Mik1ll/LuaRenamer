using System;
using System.Globalization;
using AwesomeAssertions;
using Xunit;

namespace LuaRenamer.Tests.Pure;

/// <summary>
/// Proves a CI leg configured for a non-default culture or timezone actually runs the suite under it. Only the
/// test process can check this: the shell's <c>locale</c> says nothing about what the .NET host resolved.
/// </summary>
public class HostConfigurationTests
{
    private const string CultureVariable = "LUARENAMER_EXPECT_CULTURE";
    private const string TimeZoneVariable = "LUARENAMER_EXPECT_TZ";

    [Fact]
    public void TheProcessRunsUnderTheExpectedCulture()
    {
        var expected = Expectation(CultureVariable);
        CultureInfo.CurrentCulture.Name.Should().Be(expected);
    }

    [Fact]
    public void TheProcessRunsUnderTheExpectedTimeZone()
    {
        var expected = Expectation(TimeZoneVariable);
        TimeZoneInfo.Local.Id.Should().Be(expected);
    }

    private static string Expectation(string variable)
    {
        var expected = Environment.GetEnvironmentVariable(variable);
        // A skip rather than a pass: an unconfigured run has checked nothing, and should not be counted as having done so.
        if (string.IsNullOrEmpty(expected))
            Assert.Skip($"{variable} is not set, so no host configuration is expected");
        return expected;
    }
}
