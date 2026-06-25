using System;
using NLua;

namespace LuaRenamer.LuaEnv.BaseTypes;

/// <summary>
/// A typed handle around the runtime <see cref="LuaTable"/> holding the name-to-name mapping for the
/// enum <typeparamref name="T"/> (produced by <c>EnumToTable</c>).
/// </summary>
public readonly struct LuaEnumRef<T>(LuaTable table) where T : struct, Enum
{
    public LuaTable Table { get; } = table;
}
