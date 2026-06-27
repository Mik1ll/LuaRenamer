using System;
using System.Collections.Generic;
using System.Linq;

namespace LuaRenamer.LuaEnv.Prototype;

/// <summary>
/// Builds the identity name→name map for an enum, the model-architecture counterpart of
/// <c>LuaContext.EnumToTable&lt;T&gt;</c>. The result is an <c>IReadOnlyDictionary&lt;T, T&gt;</c>; the
/// serializer marshals every key and value to its enum name, giving the Lua <c>{ Name = "Name", ... }</c>
/// table (so a script can write <c>env.AnimeType.Movie</c> and get the string <c>"Movie"</c>).
/// </summary>
public static class EnumTableProducer
{
    // Distinct() dedupes values that share an underlying number (mirrors LuaContext.EnumToTable),
    // so aliased names collapse to the one canonical Enum.GetName returns.
    public static IReadOnlyDictionary<T, T> EnumTable<T>() where T : struct, Enum =>
        Enum.GetValues<T>().Distinct().ToDictionary(v => v, v => v);
}
