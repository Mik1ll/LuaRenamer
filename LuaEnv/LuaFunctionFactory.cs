using System;
using NLua;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// Owns construction of every <see cref="LuaFunctionDef{TDelegate}"/> wrapper: one dedicated method per
/// function class. Each runs the function's Lua source through the sandbox and re-homes the resulting handle
/// as the typed wrapper. The function classes themselves are dumb carriers (internal ctor only) — all the
/// wiring lives here.
/// </summary>
/// <remarks>
/// Re-homing: the run handle and the returned wrapper share one Lua registry reference
/// (<see cref="object.GetHashCode"/> returns the ref), so ownership transfers to the wrapper — the run
/// handle's finalizer is suppressed to avoid unref-ing it out from under the wrapper.
/// </remarks>
public static class LuaFunctionFactory
{
    public static AnimeGetName CreateAnimeGetName(LuaFunction runSandboxed, LuaTable env, Lua interpreter)
    {
        var fn = (LuaFunction)runSandboxed.Call(GetNameSource.Lua, env)[1];
        GC.SuppressFinalize(fn);
        return new AnimeGetName(fn.GetHashCode(), interpreter);
    }

    public static TitleGetName CreateTitleGetName(LuaFunction runSandboxed, LuaTable env, Lua interpreter)
    {
        var fn = (LuaFunction)runSandboxed.Call(GetNameSource.Lua, env)[1];
        GC.SuppressFinalize(fn);
        return new TitleGetName(fn.GetHashCode(), interpreter);
    }
}
