using System.ComponentModel;
using System.Reflection;
using System.Text;
using LuaRenamer.LuaEnv;
using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaDefsGenerator;

public class Generator
{
    private readonly string _outputPath;

    // Maps CLR enum type -> Lua name (e.g. typeof(AnimeType) -> "AnimeType").
    // Built once from EnumsTable's LuaEnumRef<T> instance properties.
    private static readonly Dictionary<Type, string> EnumToLuaName = BuildEnumMap();

    public Generator(string outputPath) => _outputPath = Path.GetFullPath(outputPath);

    public void GenerateDefinitionFiles()
    {
        GenerateDefsFile();
        GenerateEnumsFile();
        GenerateEnvFile();
    }

    private static Dictionary<Type, string> BuildEnumMap()
    {
        var result = new Dictionary<Type, string>();
        foreach (var prop in typeof(EnvTable).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.PropertyType.IsGenericType &&
                                 p.PropertyType.GetGenericTypeDefinition() == typeof(LuaEnumRef<>)))
        {
            var enumType = prop.PropertyType.GetGenericArguments()[0];
            result[enumType] = prop.Name;
        }
        return result;
    }

    private void GenerateDefsFile()
    {
        var ctx = new NullabilityInfoContext();
        var types = typeof(Table).Assembly.DefinedTypes
            .Select(t => new { LuaTypeAttribute = t.GetCustomAttribute<LuaTypeAttribute>(), Type = t })
            .Where(t => t.LuaTypeAttribute is not null)
            .OrderBy(t => t.LuaTypeAttribute!.Type, StringComparer.Ordinal)
            .ToList();
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");

        foreach (var type in types)
        {
            var className = type.LuaTypeAttribute!.Type;
            var functions = new List<(PropertyInfo prop, LuaFieldAttribute fieldAttr)>();
            sb.Append($"---@class (exact) {className}\n");

            foreach (var member in type.Type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                if (member.GetCustomAttribute<LuaTypeAttribute>() is { } typeAttr)
                {
                    sb.Append($"---@field {member.Name} {typeAttr.Type}{(typeAttr.Description is { } desc ? $" # {desc}" : string.Empty)}\n");
                }
                else if (member is PropertyInfo prop && prop.GetCustomAttribute<LuaFieldAttribute>() is { } fieldAttr)
                {
                    if (prop.PropertyType.IsGenericType &&
                        prop.PropertyType.GetGenericTypeDefinition() == typeof(LuaFunctionRef<>))
                    {
                        functions.Add((prop, fieldAttr));
                    }
                    else
                    {
                        var luaType = InferLuaType(prop, ctx);
                        sb.Append($"---@field {member.Name} {luaType}{(fieldAttr.Description is { } desc ? $" # {desc}" : string.Empty)}\n");
                    }
                }
            }

            sb.Append($"local {className} = {{}}\n\n");

            foreach (var func in functions)
                GenerateFunctionAnnotations(sb, func.prop, func.fieldAttr, $"{className}:{func.prop.Name}", ctx);
        }

        sb.Length--;

        File.WriteAllText(Path.Combine(_outputPath, "defs.lua"), sb.ToString());
    }

    private static string InferLuaType(PropertyInfo prop, NullabilityInfoContext ctx)
    {
        var t = prop.PropertyType;

        // Nullable<T> struct wrapper (LuaRef<T>?, long?, bool?, enum?)
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var inner = t.GetGenericArguments()[0];
            return InferLuaTypeForInner(inner) + "|nil";
        }

        // Nullable reference type (string?)
        var nullInfo = ctx.Create(prop);
        var isNullableRef = t.IsClass && (nullInfo.ReadState == NullabilityState.Nullable || nullInfo.WriteState == NullabilityState.Nullable);

        return InferLuaTypeForInner(t) + (isNullableRef ? "|nil" : "");
    }

    private static string InferLuaTypeForArg(Type t, NullabilityInfo? nullInfo)
    {
        // Nullable<T> value types (bool?, long?, enum?)
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            return InferLuaTypeForInner(t.GetGenericArguments()[0]) + "|nil";
        // Nullable reference types (string?)
        var isNullRef = nullInfo != null &&
                        (nullInfo.ReadState == NullabilityState.Nullable || nullInfo.WriteState == NullabilityState.Nullable);
        return InferLuaTypeForInner(t) + (isNullRef ? "|nil" : "");
    }

    private static string InferLuaTypeForInner(Type t)
    {
        if (t == typeof(long)) return LuaTypeNames.integer;
        if (t == typeof(double)) return LuaTypeNames.number;
        if (t == typeof(bool)) return LuaTypeNames.boolean;
        if (t == typeof(string)) return LuaTypeNames.@string;

        if (t.IsEnum)
            return EnumToLuaName.TryGetValue(t, out var name) ? name : t.Name;

        if (!t.IsGenericType)
            return LuaTypeNames.table;

        var def = t.GetGenericTypeDefinition();
        var args = t.GetGenericArguments();

        if (def == typeof(LuaRef<>))
        {
            var tableType = args[0];
            var luaTypeAttr = tableType.GetCustomAttribute<LuaTypeAttribute>();
            return luaTypeAttr?.Type ?? tableType.Name;
        }

        if (def == typeof(LuaArray<>))
        {
            var elem = args[0];
            return InferLuaTypeForInner(elem) + "[]";
        }

        if (def == typeof(LuaMap<,>))
            return $"table<{InferLuaTypeForInner(args[0])}, {InferLuaTypeForInner(args[1])}>";

        if (def == typeof(LuaUnion<,>))
            return InferLuaTypeForInner(args[0]) + "|" + InferLuaTypeForInner(args[1]);

        return LuaTypeNames.table;
    }

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

    private void GenerateEnumsFile()
    {
        var enumsType = typeof(EnvTable);
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");
        foreach (var prop in enumsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.PropertyType.IsGenericType &&
                                 p.PropertyType.GetGenericTypeDefinition() == typeof(LuaEnumRef<>)))
        {
            var enumType = prop.PropertyType.GenericTypeArguments[0];
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

        File.WriteAllText(Path.Combine(_outputPath, "enums.lua"), sb.ToString());
        return;

        void CreateMappings(IEnumerable<string> enumerable)
        {
            foreach (var name in enumerable)
                sb.Append($"    {name} = \"{name}\",\n");
        }
    }

    private static void GenerateFunctionAnnotations(StringBuilder sb, PropertyInfo prop, LuaFieldAttribute fieldAttr, string functionName,
        NullabilityInfoContext ctx)
    {
        if (fieldAttr.Description is { } description)
            sb.Append($"---{description}\n");

        var delegateType = prop.PropertyType.GetGenericArguments()[0]; // TDelegate from LuaFunctionRef<TDelegate>
        var invoke = delegateType.GetMethod("Invoke")!;
        var parameters = invoke.GetParameters();

        foreach (var param in parameters)
        {
            var nullInfo = ctx.Create(param);
            var luaType = InferLuaTypeForArg(param.ParameterType, nullInfo);
            var desc = param.GetCustomAttribute<DescriptionAttribute>()?.Description;
            var suffix = desc is not null ? $" # {desc}" : "";
            sb.Append($"---@param {param.Name} {luaType}{suffix}\n");
        }

        var retType = invoke.ReturnType;
        if (retType != typeof(void))
        {
            var retNullInfo = ctx.Create(invoke.ReturnParameter);
            sb.Append($"---@return {InferLuaTypeForArg(retType, retNullInfo)}\n");
        }
        else
        {
            sb.Append("---@return nil\n");
        }

        sb.Append($"function {functionName}({string.Join(", ", parameters.Select(p => p.Name))}) end\n\n");
    }

    private void GenerateEnvFile()
    {
        var ctx = new NullabilityInfoContext();
        var envType = typeof(EnvTable);
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");

        foreach (var member in envType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                                .Where(p => !p.PropertyType.IsGenericType || p.PropertyType.GetGenericTypeDefinition() != typeof(LuaEnumRef<>)))
        {
            if (member.GetCustomAttribute<LuaTypeAttribute>() is { } typeAttr)
            {
                if (typeAttr.Description is { } description)
                    sb.Append($"---{description}\n");
                sb.Append($"---@type {typeAttr.Type}\n");
                sb.Append($"{member.Name} = {typeAttr.DefaultValue}\n\n");
            }
            else if (member is PropertyInfo prop && prop.GetCustomAttribute<LuaFieldAttribute>() is { } fieldAttr)
            {
                if (prop.PropertyType.IsGenericType &&
                    prop.PropertyType.GetGenericTypeDefinition() == typeof(LuaFunctionRef<>))
                {
                    GenerateFunctionAnnotations(sb, prop, fieldAttr, prop.Name, ctx);
                }
                else
                {
                    var luaType = InferLuaType(prop, ctx);
                    if (fieldAttr.Description is { } desc)
                        sb.Append($"---{desc}\n");
                    sb.Append($"---@type {luaType}\n");
                    sb.Append($"{prop.Name} = {fieldAttr.DefaultValue}\n\n");
                }
            }
        }

        sb.Length--;

        File.WriteAllText(Path.Combine(_outputPath, "env.lua"), sb.ToString());
    }
}
