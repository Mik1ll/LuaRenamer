using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.Prototype;

namespace LuaRenamer.DefsGenerator;

/// <summary>
/// Prototype of the <c>*Names</c> navigation DSL, driven by the <see cref="ILuaModel"/> records
/// instead of the <c>*Table</c> writer classes. This is the model-architecture counterpart of the
/// Roslyn <c>NamesGenerator</c>; it reflects plain CLR types rather than matching on the
/// <c>LuaRef</c>/<c>LuaArray</c>/<c>LuaMap</c>/<c>LuaEnumRef</c> symbol names. Output is byte-identical
/// to the live generator for the ported types.
/// </summary>
/// <remarks>
/// Reflection-based (like <see cref="ModelDefsGenerator"/>) so it can run standalone for verification;
/// the production version would be the Roslyn generator re-pointed at <see cref="ILuaModel"/> symbols.
/// </remarks>
public class ModelNamesGenerator
{
    private const string Bt = "global::LuaRenamer.LuaEnv.Names";

    private static bool IsGenericDef(Type t, Type def) => t.IsGenericType && t.GetGenericTypeDefinition() == def;
    private static string StripModel(string name) => name.EndsWith("Model") ? name[..^5] : name;
    private static string NamesOf(Type t) => StripModel(t.Name) + "Names";

    public string GenerateNames()
    {
        var ctx = new NullabilityInfoContext();
        var types = typeof(ILuaModel).Assembly.DefinedTypes
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ILuaModel).IsAssignableFrom(t))
            .OrderBy(t => StripModel(t.Name), StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        foreach (var type in types)
            EmitNames(sb, type, ctx);
        return sb.ToString();
    }

    public string EmitClass(Type type) // single-class helper for verification
    {
        var sb = new StringBuilder();
        EmitNames(sb, type, new NullabilityInfoContext());
        return sb.ToString();
    }

    private static void EmitNames(StringBuilder sb, Type type, NullabilityInfoContext ctx)
    {
        sb.Append($"public sealed class {NamesOf(type)} : {Bt}.Table\n{{\n");

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<LuaFieldAttribute>() is not { } fieldAttr)
                continue;

            // LuaFn<TDelegate> -> a callable member rendered inline in declaration order; ':' if Method else '.'.
            if (IsGenericDef(prop.PropertyType, typeof(LuaFn<>)))
                sb.Append(FuncMember(prop, fieldAttr, ctx));
            else
                sb.Append(NavMember(prop));
        }

        sb.Append("}\n\n");
    }

    private static string NavMember(PropertyInfo prop)
    {
        var name = prop.Name;
        var pt = prop.PropertyType;

        // IReadOnlyList<TModel> (or array/collection of models) -> ArrayTable<TModelNames>
        if (ListElement(pt) is { } elem && typeof(ILuaModel).IsAssignableFrom(elem))
            return $"    public {Bt}.ArrayTable<{NamesOf(elem)}> {name} => new() {{ Fn = Get() }};\n";

        // Nested model (incl. nullable ref like DateTimeModel?) -> navigable nav property
        if (typeof(ILuaModel).IsAssignableFrom(pt))
            return $"    public {NamesOf(pt)} {name} => new() {{ Fn = Get() }};\n";

        // Everything else (scalars, enums, scalar lists, dictionaries) -> leaf string path
        return $"    public string {name} => Get();\n";
    }

    private static string FuncMember(PropertyInfo prop, LuaFieldAttribute fieldAttr, NullabilityInfoContext ctx)
    {
        var sep = fieldAttr.Method ? ":" : ".";
        var delegateType = prop.PropertyType.GetGenericArguments()[0];
        var invoke = delegateType.GetMethod("Invoke")!;

        var pars = new List<string>();
        var args = new List<string>();
        foreach (var param in invoke.GetParameters())
        {
            var ni = ctx.Create(param);
            var isNull = IsGenericDef(param.ParameterType, typeof(Nullable<>))
                || ni is { ReadState: NullabilityState.Nullable } or { WriteState: NullabilityState.Nullable };
            pars.Add(isNull ? $"string? {param.Name} = null" : $"string {param.Name}");
            args.Add(param.Name!);
        }

        return $"    public string {prop.Name}({string.Join(", ", pars)}) => GetFunc([{string.Join(", ", args)}], '{sep}');\n";
    }

    private static Type? ListElement(Type t)
    {
        if (t.IsArray)
            return t.GetElementType();
        if (t.IsGenericType)
        {
            var def = t.GetGenericTypeDefinition();
            if (def == typeof(IReadOnlyList<>) || def == typeof(IReadOnlyCollection<>) || def == typeof(IEnumerable<>))
                return t.GetGenericArguments()[0];
        }
        return null;
    }
}
