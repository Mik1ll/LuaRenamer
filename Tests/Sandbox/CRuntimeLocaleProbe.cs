using System;
using System.Globalization;
using LuaRenamer.LuaEnv;
using Xunit;

namespace LuaRenamer.Tests.Sandbox;

// Throwaway probe for ci-platform-matrix task 3.2; reverted once each leg's result is recorded.
public sealed class CRuntimeLocaleProbe : IDisposable
{
    private readonly LuaSandbox _sandbox = new(LuaScripts.LuaLinq, LuaScripts.Utils);

    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public void Probe()
    {
        var error = _sandbox.Run("probe_num = tostring(1.5); probe_date = os.date('%x', 0); probe_full = os.date('%c', 0)");
        Assert.Skip(
            $"PROBE culture={CultureInfo.CurrentCulture.Name} tz={TimeZoneInfo.Local.Id} " +
            $"LANG={Environment.GetEnvironmentVariable("LANG")} LC_ALL={Environment.GetEnvironmentVariable("LC_ALL")} " +
            $"dotnet(1.5)={1.5.ToString(CultureInfo.CurrentCulture)} error={error} " +
            $"lua tostring(1.5)={_sandbox.GetValue("probe_num")} os.date('%x',0)={_sandbox.GetValue("probe_date")} " +
            $"os.date('%c',0)={_sandbox.GetValue("probe_full")}");
    }
}
