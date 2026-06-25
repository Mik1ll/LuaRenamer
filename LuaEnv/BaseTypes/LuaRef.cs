using NLua;

namespace LuaRenamer.LuaEnv.BaseTypes;

/// <summary>
/// A typed handle around a runtime <see cref="LuaTable"/> whose shape corresponds to the schema
/// table <typeparamref name="T"/>. Produced by the <c>*ToTable</c> builders in LuaContext and
/// consumed by the generated table builders to give compile-time structural type safety.
/// </summary>
public readonly struct LuaRef<T>(LuaTable table) where T : LuaTableWriter
{
    public LuaTable Table { get; } = table;
}
