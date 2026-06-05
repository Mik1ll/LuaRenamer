using NLua;

namespace LuaRenamer.LuaEnv.BaseTypes;

/// <summary>
/// A typed handle around a runtime sequence <see cref="LuaTable"/> (1-based array) whose elements
/// are of type <typeparamref name="TElem"/> (a scalar, an enum, or a <see cref="LuaRef{T}"/>).
/// </summary>
public readonly struct LuaArray<TElem>(LuaTable table)
{
    public LuaTable Table { get; } = table;
}
