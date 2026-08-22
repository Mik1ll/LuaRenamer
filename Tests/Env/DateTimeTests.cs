using System;
using AwesomeAssertions;
using LuaRenamer.LuaEnv;
using LuaRenamer.LuaEnv.Models;
using LuaRenamer.Tests.Fakes;
using Xunit;

namespace LuaRenamer.Tests.Env;

/// <summary>
/// Dates, asserted on the discrete fields the producer actually computes and — for the script-facing side —
/// through explicit format specifiers. Nothing here goes through a locale-defined composite format such as
/// <c>%c</c>, whose layout is the C library's rather than this project's, and which would also pin an
/// <c>isdst</c> value at the hour most likely to sit on a transition in some zone.
/// </summary>
public sealed class DateTimeTests : IDisposable
{
    private readonly LuaSandbox _sandbox = new(LuaScripts.LuaLinq, LuaScripts.Utils);

    public void Dispose() => _sandbox.Dispose();

    private static DateTimeModel? ReleaseDate(DateOnly? released) =>
        ModelProducers.FileToModel(HostFakes.FileReleasedOn(released)).anidb!.releasedate;

    [Fact]
    public void ADateMapsFieldByField()
    {
        DateTimeModel date = ReleaseDate(new DateOnly(2022, 2, 3))!;

        date.year.Should().Be(2022);
        date.month.Should().Be(2);
        date.day.Should().Be(3);
        date.yday.Should().Be(34);
        date.wday.Should().Be(5, "Lua numbers the week from 1 = Sunday, so Thursday is 5");
        date.hour.Should().Be(0);
        date.min.Should().Be(0);
        date.sec.Should().Be(0);
    }

    [Fact]
    public void ADateWithNoValueProducesNoDateModel() => ReleaseDate(null).Should().BeNull();

    [Fact]
    public void ScriptSideFormattingUsesExplicitSpecifiers()
    {
        // os.time reads the table as local time and os.date renders in local time, so the round trip is
        // zone-independent — but only because every rendered component is named here rather than left to
        // the C library's locale-defined layout.
        _sandbox.Env["releasedate"] = new ModelTranslator(_sandbox).Translate(ReleaseDate(new DateOnly(2022, 2, 3))!);
        _sandbox.Run("rendered = os.date('%Y-%m-%d %H:%M:%S', os.time(releasedate))").Should().BeNull();

        _sandbox.GetValue("rendered").Should().Be("2022-02-03 00:00:00");
    }
}
