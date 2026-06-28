using System;
using NLua;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// Marker for a plain-C# node in the env description graph. Implementing types are records whose
/// <c>[LuaField]</c> properties use ordinary CLR types (scalars, enums, <see cref="ILuaModel"/>,
/// <c>IReadOnlyList&lt;T&gt;</c>, <c>IReadOnlyDictionary&lt;K,V&gt;</c>, a CLR delegate, or a
/// <see cref="LuaFunctionDef{TDelegate}"/>). The graph carries no <see cref="LuaTable"/>;
/// materialization is deferred to <see cref="LuaSerializer"/>.
/// </summary>
public interface ILuaModel;

/// <summary>
/// Base for a callable whose body is written in Lua and bound into the env (currently just
/// <see cref="GetName"/>). It <em>is</em> a <see cref="LuaFunction"/> — the live handle the serializer
/// drops into a table slot as-is — while <typeparamref name="TDelegate"/> carries the call signature the
/// defs/names generators read (parameter names/types, <c>[Description]</c>s, return type).
/// </summary>
/// <remarks>
/// Free functions supplied by the host are plain CLR delegates and need no carrier — their field is typed
/// as the delegate directly. This base exists only for callables whose body lives in Lua, where the runtime
/// value must be a real <see cref="LuaFunction"/> handle yet must still expose a typed signature. The '.'
/// vs ':' call-syntax distinction stays a generator concern (a <c>[LuaField(Method = …)]</c> flag), not a
/// runtime one.
/// </remarks>
public abstract class LuaFunctionDef<TDelegate> : LuaFunction where TDelegate : Delegate
{
    protected LuaFunctionDef(int reference, Lua interpreter) : base(reference, interpreter) { }
}
