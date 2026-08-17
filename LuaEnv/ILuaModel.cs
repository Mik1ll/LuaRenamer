using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// Marker for a plain-C# node in the env description graph. Implementing types are records whose
/// <c>[LuaField]</c> properties use ordinary CLR types (scalars, enums, <see cref="ILuaModel"/>,
/// <c>IReadOnlyList&lt;T&gt;</c>, <c>IReadOnlyDictionary&lt;K,V&gt;</c>, <see cref="LuaEnumTable{TEnum}"/>,
/// a CLR delegate, or a <see cref="LuaFunc{TDelegate}"/>). The graph carries no <c>LuaTable</c>;
/// materialization is deferred to <see cref="ModelTranslator"/>.
/// </summary>
public interface ILuaModel;

/// <summary>
/// The one definition of "which properties of a model are Lua fields, and in what order". Both the runtime
/// serializer (<see cref="ModelTranslator"/>) and the build-time schema emitters read the graph through here,
/// so the two views cannot disagree about a model's shape.
/// </summary>
public static class LuaSchema
{
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Prop, LuaFieldAttribute Field)[]> Cache = new();

    /// <summary>
    /// The <c>[LuaField]</c>-marked properties of a model, in declaration order. Cached per type — a single env
    /// graph has hundreds of nodes, and the reflection is identical for every node of a given type.
    /// </summary>
    /// <remarks>
    /// Static properties are included: a Lua-bodied callable is the same for every node of a type, so it is
    /// declared static rather than threaded through every producer.
    /// </remarks>
    public static IReadOnlyList<(PropertyInfo Prop, LuaFieldAttribute Field)> LuaFields(Type t) =>
        Cache.GetOrAdd(t, static type => [.. type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Select(p => (Prop: p, Field: p.GetCustomAttribute<LuaFieldAttribute>()!))
            .Where(x => x.Field is not null)]);
}
