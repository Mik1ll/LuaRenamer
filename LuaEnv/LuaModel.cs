using System;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// Marker for a plain-C# node in the env description graph. Implementing types are records whose
/// <c>[LuaField]</c> properties use ordinary CLR types (scalars, enums, <see cref="ILuaModel"/>,
/// <c>IReadOnlyList&lt;T&gt;</c>, <c>IReadOnlyDictionary&lt;K,V&gt;</c>, a CLR delegate, or a
/// <see cref="LuaFunctionDef{TDelegate}"/>). The graph carries no <c>LuaTable</c>;
/// materialization is deferred to <see cref="ModelTranslator"/>.
/// </summary>
public interface ILuaModel;

/// <summary>
/// Describes a callable whose body is written in Lua and bound into the env (currently
/// <see cref="AnimeGetName"/> and <see cref="TitleGetName"/>). It is a pure description — just the
/// <see cref="Source"/> code — carrying <em>no</em> live Lua handle and no interpreter. The actual
/// <c>LuaFunction</c> is minted on demand by <see cref="ModelTranslator"/> when it reaches the field, so the
/// model graph stays decoupled from any <c>Lua</c> instance.
/// </summary>
/// <remarks>
/// Free functions supplied by the host are plain CLR delegates and need no carrier — their field is typed
/// as the delegate directly. This base exists only for callables whose body lives in Lua, where the
/// generic <see cref="LuaFunctionDef{TDelegate}"/> additionally pins the call signature the defs/names
/// generators read (parameter names/types, <c>[Description]</c>s, return type). The '.' vs ':' call-syntax
/// distinction stays a generator concern (a <c>[LuaField(Method = …)]</c> flag), not a runtime one.
/// </remarks>
public abstract class LuaFunctionDef
{
    /// <summary>The Lua source whose top-level <c>return function ... end</c> yields this callable.</summary>
    public abstract string Source { get; }
}

/// <inheritdoc cref="LuaFunctionDef"/>
/// <typeparam name="TDelegate">The call signature exposed to the generators.</typeparam>
public abstract class LuaFunctionDef<TDelegate> : LuaFunctionDef where TDelegate : Delegate;
