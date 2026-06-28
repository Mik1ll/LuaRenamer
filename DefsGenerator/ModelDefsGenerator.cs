using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LuaRenamer.LuaEnv;
using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.DefsGenerator;

/// <summary>
/// Generates the three definition files (defs.lua / enums.lua / env.lua) from the <see cref="ILuaModel"/>
/// records. The type→Lua mapping reads plain CLR types (IReadOnlyList&lt;T&gt;, IReadOnlyDictionary&lt;K,V&gt;,
/// nested models, enums, <see cref="LuaFn{T}"/>, <see cref="LuaUnion{T1,T2}"/>).
/// </summary>
/// <remarks>
/// A single class because defs/enums/env all share one type-inference routine and one enum-name map. The
/// enum map and the env/enums sections are sourced from <see cref="EnvModel"/>'s enum-table dictionaries.
/// </remarks>
public class ModelDefsGenerator
{
    // enum CLR type -> exposed Lua name (e.g. TitleLanguage -> "Language"), sourced from EnvModel's
    // enum-table properties (IReadOnlyDictionary<TEnum, TEnum>).
    private static readonly Dictionary<Type, string> EnumToLuaName =
        EnumTableProps().ToDictionary(p => p.PropertyType.GetGenericArguments()[0], p => p.Name);

    private static bool IsGenericDef(Type t, Type def) => t.IsGenericType && t.GetGenericTypeDefinition() == def;

    private static string StripModel(string name) => name.EndsWith("Model") ? name[..^5] : name;

    // An enum table is IReadOnlyDictionary<TEnum, TEnum> (key type == value type and an enum). The
    // matching key/value enum carries the CLR type the generators need; it serializes to { Name = "Name" }.
    private static bool IsEnumTable(PropertyInfo p)
    {
        if (!IsGenericDef(p.PropertyType, typeof(IReadOnlyDictionary<,>)))
            return false;
        var args = p.PropertyType.GetGenericArguments();
        return args[0] == args[1] && args[0].IsEnum;
    }

    // EnvModel's enum-table properties, in declaration order (one per exposed Lua enum).
    private static IEnumerable<PropertyInfo> EnumTableProps() =>
        typeof(EnvModel).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(IsEnumTable);

    /// <summary>
    /// Writes defs.lua / enums.lua / env.lua to <paramref name="outputPath"/>. The model-architecture
    /// replacement for <c>Generator.GenerateDefinitionFiles</c> — same output, sourced from the
    /// <see cref="ILuaModel"/> records.
    /// </summary>
    public void GenerateDefinitionFiles(string outputPath)
    {
        var dir = Path.GetFullPath(outputPath);
        File.WriteAllText(Path.Combine(dir, "defs.lua"), GenerateDefs());
        File.WriteAllText(Path.Combine(dir, "enums.lua"), GenerateEnums());
        File.WriteAllText(Path.Combine(dir, "env.lua"), GenerateEnv());
    }

    // ---- defs.lua ------------------------------------------------------------------------------

    public string GenerateDefs()
    {
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");
        sb.Append(GenerateClassSection());
        sb.Length--; // mirror Generator: drop the trailing newline of the final block
        return sb.ToString();
    }

    public string GenerateClassSection()
    {
        var ctx = new NullabilityInfoContext();
        var types = typeof(ILuaModel).Assembly.DefinedTypes
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ILuaModel).IsAssignableFrom(t) && t != typeof(EnvModel))
            .OrderBy(t => StripModel(t.Name), StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();

        foreach (var type in types)
        {
            var className = StripModel(type.Name);
            var functions = new List<(PropertyInfo prop, LuaFieldAttribute fieldAttr, string sep)>();
            sb.Append($"---@class (exact) {className}\n");

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetCustomAttribute<LuaFieldAttribute>() is not { } fieldAttr)
                    continue;

                // LuaFn<TDelegate> -> deferred to the function section; ':' if Method else '.'.
                if (IsGenericDef(prop.PropertyType, typeof(LuaFn<>)))
                    functions.Add((prop, fieldAttr, fieldAttr.Method ? ":" : "."));
                else
                {
                    var luaType = InferLuaType(prop.PropertyType, ctx.Create(prop));
                    sb.Append($"---@field {prop.Name} {luaType}{(fieldAttr.Description is { } d ? $" # {d}" : "")}\n");
                }
            }

            sb.Append($"local {className} = {{}}\n\n");

            foreach (var (prop, fieldAttr, sep) in functions)
                GenerateFunctionAnnotations(sb, prop, fieldAttr, $"{className}{sep}{prop.Name}", ctx);
        }

        return sb.ToString();
    }

    // ---- enums.lua -----------------------------------------------------------------------------

    public string GenerateEnums()
    {
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");
        foreach (var prop in EnumTableProps())
        {
            var enumType = prop.PropertyType.GetGenericArguments()[0];
            var propName = prop.Name;

            sb.Append($"---@enum {propName}\n");
            sb.Append($"{propName} = {{\n");

            if (enumType == typeof(TitleLanguage))
            {
                var lkup = Enum.GetValues<TitleLanguage>().ToLookup(t => t switch
                {
                    TitleLanguage.Japanese or TitleLanguage.Romaji or TitleLanguage.English or TitleLanguage.Chinese or TitleLanguage.Pinyin
                        or TitleLanguage.Korean or TitleLanguage.KoreanTranscription => 0,
                    TitleLanguage.Unknown or TitleLanguage.Main or TitleLanguage.None => 3,
                    _ => AnidbLangs.Contains(t) ? 1 : 2,
                }, t => t.ToString());
                sb.Append("\n--#region AniDB Languages\n");
                CreateMappings(lkup[0]);
                sb.Append('\n');
                CreateMappings(lkup[1].Order(StringComparer.Ordinal));
                sb.Append("--#endregion\n");
                sb.Append("\n--#region Other Languages\n");
                CreateMappings(lkup[2].Order(StringComparer.Ordinal));
                sb.Append("--#endregion\n\n");
                CreateMappings(lkup[3]);
            }
            else
            {
                // Use over GetNames to prevent creating new enum values that had same value before
                CreateMappings(Enum.GetValues(enumType).Cast<object>().Distinct().Select(v => Enum.GetName(enumType, v)!));
            }

            sb.Append("}\n\n");
        }

        sb.Length--;
        return sb.ToString();

        void CreateMappings(IEnumerable<string> enumerable)
        {
            foreach (var name in enumerable)
                sb.Append($"    {name} = \"{name}\",\n");
        }
    }

    // ---- env.lua -------------------------------------------------------------------------------

    public string GenerateEnv()
    {
        var ctx = new NullabilityInfoContext();
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");

        foreach (var prop in typeof(EnvModel).GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                     .Where(p => !IsEnumTable(p)))
        {
            if (prop.GetCustomAttribute<LuaFieldAttribute>() is not { } fieldAttr)
                continue;

            if (IsGenericDef(prop.PropertyType, typeof(LuaFn<>)))
            {
                GenerateFunctionAnnotations(sb, prop, fieldAttr, prop.Name, ctx);
            }
            else
            {
                var luaType = InferLuaType(prop.PropertyType, ctx.Create(prop));
                if (fieldAttr.Description is { } desc)
                    sb.Append($"---{desc}\n");
                sb.Append($"---@type {luaType}\n");
                sb.Append($"{prop.Name} = {fieldAttr.DefaultValue}\n\n");
            }
        }

        sb.Length--;
        return sb.ToString();
    }

    // ---- shared type inference -----------------------------------------------------------------

    private static string InferLuaType(Type t, NullabilityInfo? nullInfo)
    {
        if (IsGenericDef(t, typeof(Nullable<>)))
            return InferInner(t.GetGenericArguments()[0]) + "|nil";

        var isNullableRef = nullInfo is { ReadState: NullabilityState.Nullable } or { WriteState: NullabilityState.Nullable };
        return InferInner(t) + (isNullableRef ? "|nil" : "");
    }

    private static string InferInner(Type t)
    {
        if (t == typeof(long)) return LuaTypeNames.integer;
        if (t == typeof(double)) return LuaTypeNames.number;
        if (t == typeof(bool)) return LuaTypeNames.boolean;
        if (t == typeof(string)) return LuaTypeNames.@string;

        if (t.IsEnum)
            return EnumToLuaName.TryGetValue(t, out var name) ? name : t.Name;

        if (t.IsArray)
            return InferInner(t.GetElementType()!) + "[]";

        if (t.IsGenericType)
        {
            var def = t.GetGenericTypeDefinition();
            var args = t.GetGenericArguments();
            if (def == typeof(IReadOnlyDictionary<,>))
                return $"table<{InferInner(args[0])}, {InferInner(args[1])}>";
            if (def == typeof(IReadOnlyList<>) || def == typeof(IReadOnlyCollection<>) || def == typeof(IEnumerable<>))
                return InferInner(args[0]) + "[]";
            if (def == typeof(LuaUnion<,>))
                return InferInner(args[0]) + "|" + InferInner(args[1]);
        }

        if (typeof(ILuaModel).IsAssignableFrom(t))
            return StripModel(t.Name);

        return LuaTypeNames.table;
    }

    private static void GenerateFunctionAnnotations(StringBuilder sb, PropertyInfo prop, LuaFieldAttribute fieldAttr, string functionName,
        NullabilityInfoContext ctx)
    {
        if (fieldAttr.Description is { } description)
            sb.Append($"---{description}\n");

        var delegateType = prop.PropertyType.GetGenericArguments()[0]; // TDelegate from LuaFn<TDelegate>
        var invoke = delegateType.GetMethod("Invoke")!;
        var parameters = invoke.GetParameters();

        foreach (var param in parameters)
        {
            var luaType = InferLuaType(param.ParameterType, ctx.Create(param));
            var desc = param.GetCustomAttribute<DescriptionAttribute>()?.Description;
            sb.Append($"---@param {param.Name} {luaType}{(desc is not null ? $" # {desc}" : "")}\n");
        }

        var retType = invoke.ReturnType;
        sb.Append(retType != typeof(void)
            ? $"---@return {InferLuaType(retType, ctx.Create(invoke.ReturnParameter))}\n"
            : "---@return nil\n");

        sb.Append($"function {functionName}({string.Join(", ", parameters.Select(p => p.Name))}) end\n\n");
    }

    // TitleLanguage region/ordering data, copied verbatim from Generator so enums.lua matches exactly.
    private static readonly HashSet<TitleLanguage> AnidbLangs =
    [
        TitleLanguage.Japanese,
        TitleLanguage.Romaji,
        TitleLanguage.English,
        TitleLanguage.Chinese,
        TitleLanguage.ChineseSimplified,
        TitleLanguage.ChineseTraditional,
        TitleLanguage.Pinyin,
        TitleLanguage.Korean,
        TitleLanguage.KoreanTranscription,
        TitleLanguage.Afrikaans,
        TitleLanguage.Albanian,
        TitleLanguage.Arabic,
        TitleLanguage.Bengali,
        TitleLanguage.Bosnian,
        TitleLanguage.Bulgarian,
        TitleLanguage.MyanmarBurmese,
        TitleLanguage.Croatian,
        TitleLanguage.Czech,
        TitleLanguage.Danish,
        TitleLanguage.Dutch,
        TitleLanguage.Esperanto,
        TitleLanguage.Estonian,
        TitleLanguage.Filipino,
        TitleLanguage.Finnish,
        TitleLanguage.French,
        TitleLanguage.Georgian,
        TitleLanguage.German,
        TitleLanguage.Greek,
        TitleLanguage.HaitianCreole,
        TitleLanguage.Hebrew,
        TitleLanguage.Hindi,
        TitleLanguage.Hungarian,
        TitleLanguage.Icelandic,
        TitleLanguage.Indonesian,
        TitleLanguage.Italian,
        TitleLanguage.Javanese,
        TitleLanguage.Latin,
        TitleLanguage.Latvian,
        TitleLanguage.Lithuanian,
        TitleLanguage.Malaysian,
        TitleLanguage.Mongolian,
        TitleLanguage.Nepali,
        TitleLanguage.Norwegian,
        TitleLanguage.Persian,
        TitleLanguage.Polish,
        TitleLanguage.Portuguese,
        TitleLanguage.BrazilianPortuguese,
        TitleLanguage.Romanian,
        TitleLanguage.Russian,
        TitleLanguage.Serbian,
        TitleLanguage.Sinhala,
        TitleLanguage.Slovak,
        TitleLanguage.Slovenian,
        TitleLanguage.Spanish,
        TitleLanguage.Basque,
        TitleLanguage.Catalan,
        TitleLanguage.Galician,
        TitleLanguage.Swedish,
        TitleLanguage.Tamil,
        TitleLanguage.Tatar,
        TitleLanguage.Telugu,
        TitleLanguage.Thai,
        TitleLanguage.ThaiTranscription,
        TitleLanguage.Turkish,
        TitleLanguage.Ukrainian,
        TitleLanguage.Urdu,
        TitleLanguage.Vietnamese,
    ];
}
