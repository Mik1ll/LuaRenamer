using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using AwesomeAssertions;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Video.Relocation;
using Xunit;

namespace LuaRenamer.Tests.Packaging;

/// <summary>
/// Proves a CI leg tested the archive that ships. Each expectation comes from the leg's matrix entry rather than from
/// the overlay step, so a leg that lost that step fails here instead of quietly testing a local build.
/// </summary>
public class PackagedArchiveTests
{
    private const string RuntimeVariable = "LUARENAMER_EXPECT_RID";
    private const string VersionVariable = "LUARENAMER_EXPECT_VERSION";
    private const string ArchiveVariable = "LUARENAMER_ARCHIVE";

    [Fact]
    public void TheSuiteRunsAgainstThePackagedBuild()
    {
        var expectedRuntime = Expectation(RuntimeVariable);
        var expectedVersion = Expectation(VersionVariable);
        Assembly plugin = typeof(LuaRenamer).Assembly;

        Stamped(plugin, "RuntimeIdentifier").Should().Be(expectedRuntime);
        // A Stable version has three parts, and the assembly always has four.
        plugin.GetName().Version!.ToString(expectedVersion.Split('.').Length).Should().Be(expectedVersion);
    }

    [Fact]
    public void TheArchiveRunsAScriptWhenLoadedTheWayTheHostLoadsIt()
    {
        var archive = Expectation(ArchiveVariable);
        // Never deleted: a load context holding a native library can't be unloaded, and Windows locks the file.
        DirectoryInfo extracted = Directory.CreateTempSubdirectory("luarenamer-archive-");
        ZipFile.ExtractToDirectory(archive, extracted.FullName);

        RelocationResult result = RunScript(Path.Combine(extracted.FullName, "LuaRenamer.dll"), "filename = 'from_archive'");

        result.Error.Should().BeNull();
        Path.GetFileNameWithoutExtension(result.FileName).Should().Be("from_archive");
        // By directory name, not full path: a runner's temp path can be in 8.3 form while the module's isn't.
        LoadedModules().Should().Contain(path => path.Contains(extracted.Name, StringComparison.OrdinalIgnoreCase)
                                                 && Path.GetFileName(path).Contains("lua54", StringComparison.OrdinalIgnoreCase));
    }

    private static RelocationResult RunScript(string pluginPath, string script)
    {
        Assembly plugin = new StrictPluginContext(pluginPath).LoadFromAssemblyPath(pluginPath);
        Type provider = plugin.GetType("LuaRenamer.LuaRenamer", throwOnError: true)!;
        Type settingsType = plugin.GetType("LuaRenamer.LuaRenamerSettings", throwOnError: true)!;

        var settings = Activator.CreateInstance(settingsType)!;
        settingsType.GetProperty("Script")!.SetValue(settings, script);

        // These types are the plugin's own copies, so the generic context has to be closed over them by reflection.
        RelocationContext host = RelocationGraph.Default().Context(script);
        var context = Activator.CreateInstance(typeof(RelocationContext<>).MakeGenericType(settingsType), host, settings);
        var logger = Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(provider));
        var instance = Activator.CreateInstance(provider, logger);

        return (RelocationResult)provider.GetMethod("GetPath")!.Invoke(instance, [context])!;
    }

    private static IEnumerable<string> LoadedModules() =>
        Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Select(module => module.FileName);

    private static string? Stamped(Assembly assembly, string key) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(attribute => attribute.Key == key)?.Value;

    private static string Expectation(string variable)
    {
        var expected = Environment.GetEnvironmentVariable(variable);
        // A skip rather than a pass: an unconfigured run has checked nothing, and should not be counted as having done so.
        if (string.IsNullOrEmpty(expected))
            Assert.Skip($"{variable} is not set, so no packaged build is expected");
        return expected;
    }

    /// <summary>
    /// Resolves like the host's plugin context, but refuses what the host would have to find elsewhere. This process
    /// has its own NLua, KeraLua and lua54 for the rest of the suite, and falling back to those would hide a missing file.
    /// </summary>
    private sealed class StrictPluginContext(string pluginPath) : AssemblyLoadContext(nameof(StrictPluginContext))
    {
        private static readonly HashSet<string> Framework = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => !path.StartsWith(AppContext.BaseDirectory, StringComparison.Ordinal))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

        protected override Assembly? Load(AssemblyName assemblyName) =>
            IsSuppliedByHost(assemblyName.Name!)
                ? null
                : LoadFromAssemblyPath(_resolver.ResolveAssemblyToPath(assemblyName)
                                       ?? throw new FileNotFoundException($"{assemblyName.Name} is neither in the archive nor supplied by the host"));

        protected override nint LoadUnmanagedDll(string unmanagedDllName) =>
            _resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is { } path ? LoadUnmanagedDllFromPath(path) : base.LoadUnmanagedDll(unmanagedDllName);

        private static bool IsSuppliedByHost(string name) =>
            Framework.Contains(name) || name.StartsWith("Shoko.", StringComparison.Ordinal) || name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal);
    }
}
