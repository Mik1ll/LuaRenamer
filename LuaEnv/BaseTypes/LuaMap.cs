using NLua;

namespace LuaRenamer.LuaEnv.BaseTypes;

/// <summary>
/// A typed handle around a runtime map <see cref="LuaTable"/> with keys of type
/// <typeparamref name="TKey"/> and values of type <typeparamref name="TVal"/>
/// (corresponds to a Lua <c>table&lt;K, V&gt;</c>).
/// </summary>
public readonly struct LuaMap<TKey, TVal>(LuaTable table)
{
    public LuaTable Table { get; } = table;
}
