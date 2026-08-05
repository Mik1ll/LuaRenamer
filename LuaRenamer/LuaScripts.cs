using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace LuaRenamer;

/// <summary>
/// Locates and caches the Lua files shipped alongside the plugin. The trusted chunks (<see cref="LuaLinq"/>,
/// <see cref="Utils"/>) are loaded into every sandbox env before the model fields are layered on, so they are
/// re-read from disk at most once per <see cref="CacheDuration"/> rather than per relocation.
/// </summary>
public static class LuaScripts
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);
    private static readonly Stopwatch FileCacheStopwatch = new();
    private static string? _luaUtilsText;
    private static string? _luaLinqText;

    /// <summary>The <c>lua/</c> directory next to the executing assembly.</summary>
    public static readonly string LuaPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "lua");

    /// <summary>Source of <c>lualinq.lua</c>, the query helpers (<c>from</c>, <c>where</c>, ...) user scripts rely on.</summary>
    public static string LuaLinq
    {
        get
        {
            EnsureLoaded();
            return _luaLinqText!;
        }
    }

    /// <summary>Source of <c>utils.lua</c>, the shipped helper functions layered into the env.</summary>
    public static string Utils
    {
        get
        {
            EnsureLoaded();
            return _luaUtilsText!;
        }
    }

    private static void EnsureLoaded()
    {
        if (!FileCacheStopwatch.IsRunning || FileCacheStopwatch.Elapsed > CacheDuration ||
            string.IsNullOrWhiteSpace(_luaUtilsText) ||
            string.IsNullOrWhiteSpace(_luaLinqText))
        {
            _luaUtilsText = File.ReadAllText(Path.Combine(LuaPath, "utils.lua"));
            _luaLinqText = File.ReadAllText(Path.Combine(LuaPath, "lualinq.lua"));
        }

        FileCacheStopwatch.Restart();
    }
}
