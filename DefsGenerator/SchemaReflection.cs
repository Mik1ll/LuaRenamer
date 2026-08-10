using System.Reflection;
using LuaRenamer.LuaEnv;

namespace LuaRenamer.DefsGenerator;

/// <summary>
/// The type-shape rules the schema emitters share. <see cref="ModelDefsGenerator"/> (defs.lua / enums.lua /
/// env.lua) and <see cref="ModelNamesGenerator"/> (the C# navigation DSL) answer different questions about
/// the same <see cref="ILuaModel"/> graph, but they must agree on <em>what a property is</em> — callable,
/// enum table, list, nested model — or the two views of the schema silently drift.
/// </summary>
/// <remarks>
/// Only the predicates live here; each emitter keeps its own traversal. Notably the two orderings differ on
/// purpose (defs.lua sorts by the stripped Lua class name, the DSL by the CLR type name), and
/// <see cref="LuaUnion{T1,T2}"/> is expanded by defs but stays a leaf in the DSL.
/// </remarks>
internal static class SchemaReflection
{
    internal static bool IsGenericDef(Type t, Type def) => t.IsGenericType && t.GetGenericTypeDefinition() == def;

    /// <summary>
    /// The delegate contract a callable field exposes — the delegate type itself when the implementation is
    /// C#, or <c>TDelegate</c> when it is a <see cref="LuaFunc{TDelegate}"/>. Null when the field is not
    /// callable. Its Invoke method drives every emitted signature, so both implementations document identically.
    /// </summary>
    internal static Type? ContractOf(Type t) =>
        typeof(Delegate).IsAssignableFrom(t) ? t
        : IsGenericDef(t, typeof(LuaFunc<>)) ? t.GetGenericArguments()[0]
        : null;

    /// <summary>Drops the <c>Model</c> suffix: <c>AnimeModel</c> -> <c>Anime</c>.</summary>
    internal static string StripModel(string name) => name.EndsWith("Model") ? name[..^5] : name;

    /// <summary>An enum table is a <see cref="LuaEnumTable{TEnum}"/>; <typeparamref name="TEnum"/> is the exposed enum.</summary>
    internal static bool IsEnumTable(Type t) => IsGenericDef(t, typeof(LuaEnumTable<>));

    internal static bool IsEnumTable(PropertyInfo p) => IsEnumTable(p.PropertyType);

    /// <summary><see cref="EnvModel"/>'s enum-table properties, in declaration order (one per exposed Lua enum).</summary>
    internal static IEnumerable<PropertyInfo> EnumTableProps() =>
        LuaFields(typeof(EnvModel)).Select(x => x.Prop).Where(IsEnumTable);

    /// <summary>
    /// The element type of an array / <c>IReadOnlyList&lt;T&gt;</c> / <c>IReadOnlyCollection&lt;T&gt;</c> /
    /// <c>IEnumerable&lt;T&gt;</c>, else null.
    /// </summary>
    internal static Type? ListElement(Type t)
    {
        if (t.IsArray)
            return t.GetElementType();
        if (t.IsGenericType &&
            t.GetGenericTypeDefinition() is var def &&
            (def == typeof(IReadOnlyList<>) || def == typeof(IReadOnlyCollection<>) || def == typeof(IEnumerable<>)))
            return t.GetGenericArguments()[0];
        return null;
    }

    internal static bool IsLuaModel(Type t) => typeof(ILuaModel).IsAssignableFrom(t);

    /// <summary>
    /// Every concrete <see cref="ILuaModel"/> type in the schema assembly, unordered — each emitter applies its
    /// own sort (see the remarks on this class).
    /// </summary>
    internal static IEnumerable<Type> ModelTypes() =>
        typeof(ILuaModel).Assembly.DefinedTypes.Where(t => t is { IsClass: true, IsAbstract: false } && IsLuaModel(t));

    /// <summary>The <c>[LuaField]</c>-marked properties of a model, in declaration order.</summary>
    /// <remarks>Delegates to <see cref="LuaSchema"/> so the emitters and the runtime serializer read the same fields.</remarks>
    internal static IEnumerable<(PropertyInfo Prop, LuaFieldAttribute Field)> LuaFields(Type t) => LuaSchema.LuaFields(t);
}
