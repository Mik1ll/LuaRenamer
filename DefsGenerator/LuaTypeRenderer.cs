using System.ComponentModel;
using System.Reflection;
using System.Text;
using LuaRenamer.LuaEnv;

namespace LuaRenamer.DefsGenerator;

/// <summary>
/// The Lua type vocabulary: how a CLR type becomes a LuaCATS type expression (<c>integer</c>, <c>Title[]</c>,
/// <c>table&lt;EpisodeType, integer&gt;</c>, <c>string|ImportFolder</c>, <c>…|nil</c>), and how a callable
/// becomes its <c>---@param</c>/<c>---@return</c>/<c>function … end</c> block.
/// </summary>
/// <remarks>
/// Separate from <see cref="ModelDefsGenerator"/>, which decides what the three documents contain — this
/// decides how a single type or signature reads once you are writing one. <see cref="ModelNamesGenerator"/>
/// needs none of it (it renders C#, not Lua), which is what makes this a real seam rather than a split of
/// convenience. Owns the <see cref="NullabilityInfoContext"/> the whole run shares; it is a cache, and it is
/// not thread-safe, so keep the generator single-threaded.
/// </remarks>
public sealed class LuaTypeRenderer
{
    // enum CLR type -> exposed Lua name (e.g. TitleLanguage -> "Language"), sourced from EnvModel's
    // LuaEnumTable<TEnum> properties.
    private static readonly Dictionary<Type, string> EnumToLuaName =
        SchemaReflection.EnumTableProps().ToDictionary(p => p.PropertyType.GetGenericArguments()[0], p => p.Name);

    private readonly NullabilityInfoContext _nullability = new();

    /// <summary>The LuaCATS type expression for a model property, including a <c>|nil</c> suffix when nullable.</summary>
    public string TypeOf(PropertyInfo prop) => InferLuaType(prop.PropertyType, _nullability.Create(prop));

    /// <summary>
    /// The full annotation block for a callable field — leading description, one <c>---@param</c> per
    /// parameter (with its <see cref="DescriptionAttribute"/>), the <c>---@return</c>, and the stub
    /// <c>function</c> line. <paramref name="functionName"/> carries the receiver and call syntax the caller
    /// chose (<c>Anime:getname</c> vs a bare <c>log</c>).
    /// </summary>
    public string FunctionAnnotations(PropertyInfo prop, LuaFieldAttribute fieldAttr, string functionName)
    {
        var sb = new StringBuilder();
        if (fieldAttr.Description is { } description)
            sb.Append($"---{description}\n");

        SchemaReflection.TryGetDelegateType(prop.PropertyType, out var delegateType); // the delegate itself, or TDelegate from LuaFunctionDef<TDelegate>
        var invoke = delegateType.GetMethod("Invoke")!;
        var parameters = invoke.GetParameters();

        foreach (var param in parameters)
        {
            var luaType = InferLuaType(param.ParameterType, _nullability.Create(param));
            var desc = param.GetCustomAttribute<DescriptionAttribute>()?.Description;
            sb.Append($"---@param {param.Name} {luaType}{(desc is not null ? $" # {desc}" : "")}\n");
        }

        var retType = invoke.ReturnType;
        sb.Append(retType != typeof(void)
            ? $"---@return {InferLuaType(retType, _nullability.Create(invoke.ReturnParameter))}\n"
            : "---@return nil\n");

        sb.Append($"function {functionName}({string.Join(", ", parameters.Select(p => p.Name))}) end\n\n");
        return sb.ToString();
    }

    private static string InferLuaType(Type t, NullabilityInfo? nullInfo)
    {
        if (SchemaReflection.IsGenericDef(t, typeof(Nullable<>)))
            return InferInner(t.GetGenericArguments()[0]) + "|nil";

        var isNullableRef = nullInfo is { ReadState: NullabilityState.Nullable } or { WriteState: NullabilityState.Nullable };
        return InferInner(t) + (isNullableRef ? "|nil" : "");
    }

    private static string InferInner(Type t)
    {
        if (t == typeof(long)) return "integer";
        if (t == typeof(double)) return "number";
        if (t == typeof(bool)) return "boolean";
        if (t == typeof(string)) return "string";

        if (t.IsEnum)
            return EnumToLuaName.TryGetValue(t, out var name) ? name : t.Name;

        if (SchemaReflection.ListElement(t) is { } elem)
            return InferInner(elem) + "[]";

        if (t.IsGenericType)
        {
            var def = t.GetGenericTypeDefinition();
            var args = t.GetGenericArguments();
            if (def == typeof(IReadOnlyDictionary<,>))
                return $"table<{InferInner(args[0])}, {InferInner(args[1])}>";
            if (def == typeof(LuaUnion<,>))
                return InferInner(args[0]) + "|" + InferInner(args[1]);
        }

        if (SchemaReflection.IsLuaModel(t))
            return SchemaReflection.StripModel(t.Name);

        return "table";
    }
}
