using System;
using NLua;

namespace LuaRenamer.LuaEnv.BaseTypes;

/// <summary>
/// A typed handle around a runtime <see cref="LuaTable"/> whose shape corresponds to the schema
/// table <typeparamref name="T"/>. Produced by the <c>*ToTable</c> builders in LuaContext and
/// consumed by the generated table builders to give compile-time structural type safety.
/// </summary>
public readonly struct LuaRef<T>(LuaTable table) where T : Table
{
    public LuaTable Table { get; } = table;
}

/// <summary>
/// A typed handle around the runtime <see cref="LuaTable"/> holding the name-to-name mapping for the
/// enum <typeparamref name="T"/> (produced by <c>EnumToTable</c>).
/// </summary>
public readonly struct LuaEnumRef<T>(LuaTable table) where T : struct, Enum
{
    public LuaTable Table { get; } = table;
}
