using System.Reflection;
using LuaRenamer.LuaEnv;
using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

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
    /// A callable field is either a CLR delegate (a host free function) or a <see cref="LuaFunctionDef{TDelegate}"/>
    /// subclass (a bound Lua callable carrying its signature). Both expose a delegate type whose Invoke method
    /// drives the emitted signature.
    /// </summary>
    internal static bool TryGetDelegateType(Type t, out Type delegateType)
    {
        if (typeof(Delegate).IsAssignableFrom(t))
        {
            delegateType = t;
            return true;
        }

        for (var b = t.BaseType; b != null; b = b.BaseType)
            if (IsGenericDef(b, typeof(LuaFunctionDef<>)))
            {
                delegateType = b.GetGenericArguments()[0];
                return true;
            }

        delegateType = null!;
        return false;
    }

    /// <summary>Drops the <c>Model</c> suffix: <c>AnimeModel</c> -> <c>Anime</c>.</summary>
    internal static string StripModel(string name) => name.EndsWith("Model") ? name[..^5] : name;

    /// <summary>
    /// An enum table is <c>IReadOnlyDictionary&lt;TEnum, TEnum&gt;</c> (key type == value type and an enum). The
    /// matching key/value enum carries the CLR type the generators need; it serializes to <c>{ Name = "Name" }</c>.
    /// Distinguishes the identity enum maps from scalar dictionaries like <c>illegal_chars_map</c> (&lt;string,string&gt;).
    /// </summary>
    internal static bool IsEnumTable(Type t)
    {
        if (!IsGenericDef(t, typeof(IReadOnlyDictionary<,>)))
            return false;
        var args = t.GetGenericArguments();
        return args[0] == args[1] && args[0].IsEnum;
    }

    internal static bool IsEnumTable(PropertyInfo p) => IsEnumTable(p.PropertyType);

    /// <summary><see cref="EnvModel"/>'s enum-table properties, in declaration order (one per exposed Lua enum).</summary>
    internal static IEnumerable<PropertyInfo> EnumTableProps() =>
        typeof(EnvModel).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(IsEnumTable);

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
    internal static IEnumerable<(PropertyInfo Prop, LuaFieldAttribute Field)> LuaFields(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Select(p => (Prop: p, Field: p.GetCustomAttribute<LuaFieldAttribute>()!))
            .Where(x => x.Field is not null);
}
