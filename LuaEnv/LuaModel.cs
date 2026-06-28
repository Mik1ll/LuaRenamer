using System;
using NLua;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// Marker for a plain-C# node in the env description graph. Implementing types are records whose
/// <c>[LuaField]</c> properties use ordinary CLR types (scalars, enums, <see cref="ILuaModel"/>,
/// <c>IReadOnlyList&lt;T&gt;</c>, <c>IReadOnlyDictionary&lt;K,V&gt;</c>, <see cref="LuaFn{T}"/>).
/// The graph carries no <see cref="LuaTable"/>; materialization is deferred to <see cref="LuaSerializer"/>.
/// </summary>
public interface ILuaModel;

/// <summary>
/// A bound Lua callable. The value is either a CLR delegate (bound by the host) or an existing
/// <see cref="LuaFunction"/> handle; both marshal as-is into a table slot. Unifies the old
/// LuaFunctionRef/LuaMethodRef carriers — the '.' vs ':' call-syntax distinction is a generator
/// concern (a [LuaField] flag), not a runtime one, so it is intentionally absent here.
/// </summary>
public interface ILuaCallable
{
    object Callable { get; }
}

/// <inheritdoc cref="ILuaCallable"/>
/// <remarks>
/// Closed hierarchy (pseudo discriminated union): a callable is exactly one of a host-supplied CLR
/// delegate (<see cref="Clr"/>) or an existing Lua handle (<see cref="Script"/>). The private base
/// constructor prevents outside inheritance, so the two cases are exhaustive.
/// </remarks>
public abstract record LuaFn<TDelegate> : ILuaCallable where TDelegate : Delegate
{
    private LuaFn() { } // only the nested cases below may derive

    public abstract object Callable { get; }

    public sealed record Clr(TDelegate Value) : LuaFn<TDelegate>
    {
        public override object Callable => Value;
    }

    public sealed record Script(LuaFunction Value) : LuaFn<TDelegate>
    {
        public override object Callable => Value;
    }

    public static implicit operator LuaFn<TDelegate>(TDelegate callable) => new Clr(callable);
    public static implicit operator LuaFn<TDelegate>(LuaFunction luaFunction) => new Script(luaFunction);
}
