using System;
using NLua;

namespace LuaRenamer.LuaEnv.BaseTypes;

/// <summary>
/// A unified wrapper for a Lua function binding. Carries the delegate signature in its generic
/// parameter <typeparamref name="TDelegate"/> so the source/definition generators can derive the
/// Lua parameter and return types without dedicated function attributes. Accepts either a CLR
/// delegate (bound by LuaContext) or an existing <see cref="LuaFunction"/> handle.
/// </summary>
public readonly struct LuaMethodRef<TDelegate> where TDelegate : Delegate
{
    // object because NLua accepts both delegates and LuaFunction as boxed objects for _t[key] = value.
    public object Value { get; }
    public LuaMethodRef(TDelegate callable) => Value = callable;
    public LuaMethodRef(LuaFunction luaFunction) => Value = luaFunction;
    public static implicit operator LuaMethodRef<TDelegate>(TDelegate callable) => new(callable);
    public static implicit operator LuaMethodRef<TDelegate>(LuaFunction luaFunction) => new(luaFunction);
}
